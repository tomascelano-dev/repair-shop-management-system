using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Security;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Messaging;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;

namespace RepairShop.Infrastructure.Persistence;

public static class DbSeeder
{
    /// <summary>Development credentials (seeding is OFF in Production).</summary>
    public static readonly (string Email, string DisplayName, UserRole Role, string Password)[] DevUsers =
    {
        ("admin@local", "Admin", UserRole.Admin, "Admin123456"),
        ("tech@local", "Técnico", UserRole.Tech, "Tech123456"),
        ("recepcion@local", "Recepción", UserRole.Reception, "Recepcion123"),
        ("caja@local", "Caja", UserRole.Cashier, "Caja123456"),
    };

    /// <summary>
    /// Seeds a default shop and the starter message templates (insert-missing only, so customized templates are
    /// never overwritten). The well-known demo users are only created when <paramref name="devUsers"/> is true
    /// (Development); optionally adds demo customers, stock and orders.
    /// </summary>
    public static async Task SeedAsync(
        RepairShopDbContext db,
        IDateTimeProvider clock,
        IPasswordHasher hasher,
        string shopName = "TechXto",
        bool demoData = false,
        bool devUsers = true,
        CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var shop = await EnsureShopAsync(db, clock, shopName, ct);

        if (devUsers)
        {
            foreach (var u in DevUsers)
            {
                var exists = await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Email == u.Email, ct);
                if (exists) continue;
                await db.Users.AddAsync(new AppUser(shop.Id, u.Email, u.DisplayName, u.Role, hasher.Hash(u.Password), now), ct);
            }
            await db.SaveChangesAsync(ct);
        }

        if (demoData) await SeedDemoDataAsync(db, shop, now, ct);
    }

    /// <summary>
    /// Returns the first shop, creating it when the database is empty, and makes sure every shop has the
    /// starter message templates (new template keys arrive with upgrades). Creates no users.
    /// </summary>
    public static async Task<Shop> EnsureShopAsync(RepairShopDbContext db, IDateTimeProvider clock, string shopName, CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        var shop = await db.Shops.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (shop is null)
        {
            shop = new Shop(shopName, phone: null, addressLine: null, city: "Buenos Aires", country: "AR", nowUtc: now);
            shop.UpdateOperations(90, 7, null, null, "Lunes a viernes de 10 a 19 hs", null, "7,15,30", 5, true, now);
            await db.Shops.AddAsync(shop, ct);
            await db.SaveChangesAsync(ct);
        }

        foreach (var shopId in await db.Shops.Select(s => s.Id).ToListAsync(ct))
            await SeedTemplatesAsync(db, shopId, now, ct);
        await EnsureSubscriptionsAsync(db, now, ct);
        await db.SaveChangesAsync(ct);
        return shop;
    }

    public static IReadOnlyList<(string Key, string Title, string Body)> DefaultTemplates { get; } = new (string Key, string Title, string Body)[]
    {
        // ===== ONBOARDING / RECEPCIÓN =====
        ("msg.onboarding.welcome", "Bienvenida / primer contacto",
            """
            Hola {{customer_first_name}} 👋
            Soy {{technician_name}} de {{shop_name}}.

            Decime por favor:
            1) Modelo exacto ({{device_brand}} {{device_model}})
            2) Qué le pasa (síntoma)
            3) Si tuvo golpe/humedad
            4) Si es para *hoy* o *puede esperar*

            Así te digo *precio estimado* y *turno*. ✅
            """),

        ("order.status.received", "Equipo recibido",
            """
            Hola {{customer_first_name}} 👋
            Recibimos tu {{device_brand}} {{device_model}}.
            Orden: {{order_code}} ✅

            En breve hacemos el diagnóstico y te pasamos el presupuesto.
            Seguí el estado acá: {{tracking_url}}

            — {{shop_name}}
            """),

        ("order.reception.checklist.request", "Checklist recepción (datos clave)",
            """
            Hola {{customer_first_name}} 👋
            Para avanzar con la orden {{order_code}} confirmame:

            • ¿Tenés el código/contraseña? (si aplica)
            • ¿Está desactivado Buscar iPhone / Mi Cloud / FRP?
            • ¿Querés *backup*? (puede demorar)

            Así evitamos demoras. ✅
            """),

        // ===== DIAGNÓSTICO / PRESUPUESTO =====
        ("order.diagnosis.started", "Diagnóstico en proceso",
            """
            Hola {{customer_first_name}} 👨‍🔧
            Estamos revisando tu {{device_brand}} {{device_model}}.
            Orden: {{order_code}}

            Apenas tengamos el diagnóstico te pasamos el presupuesto. ✅
            — {{shop_name}}
            """),

        ("order.quote.sent", "Presupuesto enviado",
            """
            Hola {{customer_first_name}} ✅
            Presupuesto de tu {{device_brand}} {{device_model}} (Orden {{order_code}}):

            {{quote_items}}
            Total: {{quote_amount}} {{quote_currency}}
            Válido hasta: {{quote_valid_until}}
            Garantía: {{warranty_days}} días

            Podés aprobarlo o rechazarlo acá: {{tracking_url}}
            O respondé *SI* / *NO* a este mensaje.
            """),

        ("order.quote.approved", "Presupuesto aprobado",
            """
            Genial {{customer_first_name}} ✅
            Confirmado, arrancamos con la reparación.

            Orden: {{order_code}}
            Te vamos avisando los avances. — {{shop_name}}
            """),

        ("order.quote.rejected", "Presupuesto rechazado / devolución",
            """
            Hola {{customer_first_name}} 👋
            Ok, no avanzamos con la reparación.

            Orden: {{order_code}}
            Podés retirar el equipo en {{pickup_address}} ({{pickup_hours}}).

            — {{shop_name}}
            """),

        // ===== ESTADOS =====
        ("order.status.inprogress", "En reparación",
            """
            Hola {{customer_first_name}} 👨‍🔧
            Tu {{device_brand}} {{device_model}} está en reparación.
            Orden: {{order_code}}

            Cualquier novedad te avisamos por acá. — {{shop_name}}
            """),

        ("order.status.waiting_parts", "Esperando repuesto",
            """
            Hola {{customer_first_name}} 🧩
            Tu reparación (Orden {{order_code}}) está *en espera de repuesto*.
            Apenas ingrese lo instalamos y te avisamos.

            — {{shop_name}}
            """),

        ("order.status.testing", "En pruebas",
            """
            Hola {{customer_first_name}} ✅
            Tu {{device_brand}} {{device_model}} ya fue reparado y está en *pruebas*.
            Orden: {{order_code}}

            Si todo está ok, queda listo para retirar. — {{shop_name}}
            """),

        // ===== LISTO / PAGO / RETIRO =====
        ("order.status.ready", "Listo para retirar",
            """
            Hola {{customer_first_name}} ✅
            Tu {{device_brand}} {{device_model}} ya está *listo*.
            Orden: {{order_code}}

            Total: {{order_total}}
            Pagado: {{paid_total}}
            Saldo: {{balance_due}}

            Retiro en: {{pickup_address}}
            Horario: {{pickup_hours}}

            — {{shop_name}}
            """),

        ("order.reminder.ready_pickup", "Recordatorio de retiro",
            """
            Hola {{customer_first_name}} 👋
            Te recordamos que tu {{device_brand}} {{device_model}} (Orden {{order_code}}) está listo para retirar.

            Saldo: {{balance_due}}
            Retiro en: {{pickup_address}} ({{pickup_hours}})

            — {{shop_name}}
            """),

        ("order.payment.request", "Recordatorio de pago",
            """
            Hola {{customer_first_name}} 💳
            Te pasamos el saldo de la orden {{order_code}}:

            Saldo: {{balance_due}}
            Podés pagarlo online acá: {{tracking_url}}

            Apenas se acredite te confirmamos. — {{shop_name}}
            """),

        ("order.status.delivered", "Equipo entregado",
            """
            Gracias {{customer_first_name}} 🙌
            Entregamos tu {{device_brand}} {{device_model}} (Orden {{order_code}}).

            Garantía: {{warranty_days}} días (hasta {{warranty_expires_at}}).
            Cualquier cosa escribinos por acá.

            — {{shop_name}}
            """),

        ("order.status.cancelled", "Orden cancelada",
            """
            Hola {{customer_first_name}} 👋
            La orden {{order_code}} ({{device_brand}} {{device_model}}) quedó cancelada.
            Motivo: {{cancellation_reason}}

            Podés retirar el equipo en {{pickup_address}} ({{pickup_hours}}).
            — {{shop_name}}
            """),

        // ===== GARANTÍA / POST-VENTA =====
        ("order.warranty.info", "Info de garantía",
            """
            Garantía {{shop_name}} ✅

            • Duración: {{warranty_days}} días (hasta {{warranty_expires_at}})
            • Cubre: falla del repuesto/instalación
            • No cubre: golpes, humedad, manipulaciones, software/actualizaciones

            Orden: {{order_code}}
            — {{shop_name}}
            """),

        ("order.feedback.request", "Encuesta de satisfacción",
            """
            Hola {{customer_first_name}} 🙌
            ¿Cómo te fue con la reparación de tu {{device_brand}} {{device_model}}?
            Contanos en 10 segundos: {{feedback_url}}

            ¡Gracias por elegirnos! — {{shop_name}}
            """),

        // ===== FOLLOW-UP =====
        ("followup.no_response_24h", "Seguimiento 24hs sin respuesta",
            """
            Hola {{customer_first_name}} 👋
            Te escribimos por la orden {{order_code}}.
            ¿Confirmás si avanzamos con el presupuesto? {{tracking_url}}

            Si no respondés en 48hs dejamos la orden en pausa. — {{shop_name}}
            """),
    };

    /// <summary>
    /// Organizations created by the platform owner (seed, `admin create`) get a complimentary Pro plan.
    /// Self-service signups always create their own trial, so they never reach this.
    /// </summary>
    private static async Task EnsureSubscriptionsAsync(RepairShopDbContext db, DateTime now, CancellationToken ct)
    {
        var withSubscription = await db.Subscriptions.Select(x => x.OrganizationId).ToListAsync(ct);
        var missing = await db.Shops
            .Where(s => !withSubscription.Contains(s.OrganizationId))
            .GroupBy(s => s.OrganizationId)
            .Select(g => g.Key)
            .ToListAsync(ct);
        foreach (var orgId in missing)
            await db.Subscriptions.AddAsync(Subscription.Complimentary(orgId, PlanId.Pro, "AR", now), ct);
    }

    internal static async Task SeedTemplatesAsync(RepairShopDbContext db, Guid shopId, DateTime now, CancellationToken ct)
    {
        var keys = DefaultTemplates.Select(x => x.Key).ToArray();
        var existing = (await db.MessageTemplates.IgnoreQueryFilters()
                .Where(x => x.ShopId == shopId && keys.Contains(x.Key))
                .Select(x => x.Key)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var t in DefaultTemplates.Where(t => !existing.Contains(t.Key)))
            await db.MessageTemplates.AddAsync(new MessageTemplate(shopId, t.Key, t.Title, t.Body, true, now), ct);
    }

    /// <summary>A few customers, devices, sellable stock and open orders so a fresh install is not empty.</summary>
    private static async Task SeedDemoDataAsync(RepairShopDbContext db, Shop shop, DateTime now, CancellationToken ct)
    {
        if (await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.ShopId == shop.Id, ct)) return;

        var counters = new CounterService(db);

        var stock = new (string Sku, string Name, string Category, string? Barcode, int Qty, decimal Cost, decimal Price, int MinStock, bool Sellable)[]
        {
            ("MOD-IP11", "Módulo iPhone 11 (incell)", "Pantallas", null, 3, 28000, 65000, 2, false),
            ("BAT-IP11", "Batería iPhone 11", "Baterías", null, 4, 12000, 30000, 2, false),
            ("PIN-TYPEC", "Pin de carga USB-C (genérico)", "Pines", null, 10, 1500, 6000, 5, false),
            ("ACC-CABLE-C", "Cable USB-C 1 m", "Accesorios", "7798000000017", 25, 1800, 5500, 10, true),
            ("ACC-CARG-20W", "Cargador 20 W USB-C", "Accesorios", "7798000000024", 12, 6500, 15000, 5, true),
            ("ACC-VIDRIO", "Vidrio templado (universal)", "Accesorios", "7798000000031", 40, 700, 4000, 15, true),
            ("ACC-FUNDA-IP13", "Funda silicona iPhone 13", "Accesorios", "7798000000048", 8, 2500, 9000, 3, true),
        };

        foreach (var s in stock)
        {
            var item = new InventoryItem(shop.Id, s.Sku, s.Name, s.Qty, s.Cost, "ARS", true, now);
            item.UpdateCatalog(s.Category, s.Barcode, s.MinStock, true, s.Sellable, s.Price, "ARS", s.Sellable ? 30 : 90, null, now);
            await db.InventoryItems.AddAsync(item, ct);
        }

        var people = new (string Name, string Phone, string Brand, string Model, string Issue, RepairOrderPriority Priority)[]
        {
            ("Lucía Fernández", "+54 9 11 5555-0101", "Apple", "iPhone 11", "Pantalla rota, el táctil no responde en la parte de abajo.", RepairOrderPriority.High),
            ("Martín Gómez", "+54 9 11 5555-0102", "Samsung", "Galaxy A52", "No carga, hay que mover el cable para que tome.", RepairOrderPriority.Normal),
            ("Sofía Ramírez", "+54 9 11 5555-0103", "Motorola", "Moto G60", "Se apaga solo con 30% de batería.", RepairOrderPriority.Normal),
        };

        foreach (var p in people)
        {
            var customer = new Customer(shop.Id, p.Name, p.Phone, null, now);
            var device = new Device(shop.Id, customer.Id, p.Brand, p.Model, null, null, null, now);
            var order = new RepairOrder(shop.Id, customer.Id, device.Id, p.Issue, null, now);
            order.AssignNumber(await counters.NextAsync(shop.Id, CounterKeys.RepairOrder, ct));
            order.Plan(null, p.Priority, now.AddDays(3), now);

            await db.Customers.AddAsync(customer, ct);
            await db.Devices.AddAsync(device, ct);
            await db.RepairOrders.AddAsync(order, ct);
        }

        await db.SaveChangesAsync(ct);
    }
}
