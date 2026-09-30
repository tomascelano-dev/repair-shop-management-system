using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Security;
using RepairShop.Domain.Messaging;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;

namespace RepairShop.Infrastructure.Persistence;

public static class DbSeeder
{
    /// <summary>
    /// Seeds a default Shop, users, and starter message templates.
    /// </summary>
    public static async Task SeedAsync(
        RepairShopDbContext db,
        IDateTimeProvider clock,
        IPasswordHasher hasher,
        string shopName = "TechXto",
        CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        // Ensure at least one shop exists
        var shop = await db.Shops.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (shop is null)
        {
            shop = new Shop(
                name: shopName,
                phone: null,
                addressLine: null,
                city: "Buenos Aires",
                country: "AR",
                nowUtc: now
            );
            await db.Shops.AddAsync(shop, ct);
            await db.SaveChangesAsync(ct);
        }

        // Users
        // Users (idempotent by email)
        var usersToEnsure = new (string Email, string DisplayName, UserRole Role, string Password)[]
        {
            // Default dev passwords (Seed is OFF in Production)
            ("admin@local", "Admin", UserRole.Admin, "Admin12345"),
	        // Default technician user
	        ("tech@local", "Tech", UserRole.Tech, "Tech123456")
        };

        foreach (var u in usersToEnsure)
        {
            var exists = await db.Users.AnyAsync(x => x.ShopId == shop.Id && x.Email == u.Email, ct);
            if (exists) continue;

            await db.Users.AddAsync(new AppUser(
                shopId: shop.Id,
                email: u.Email,
                displayName: u.DisplayName,
                role: u.Role,
                passwordHash: hasher.Hash(u.Password),
                nowUtc: now
            ), ct);
        }
        // Templates PRO (upsert by key, insert missing only)
        await SeedTemplatesProAsync(db, shop.Id, now, ct);

        await db.SaveChangesAsync(ct);
    }
    private static async Task SeedTemplatesProAsync(
        RepairShopDbContext db,
        Guid shopId,
        DateTime now,
        CancellationToken ct)
    {
        // Insert-missing only. Keeps any customized templates intact.
        var templates = new (string Key, string Title, string Body, bool IsActive)[]
        {
            // ===== ONBOARDING / RECEPCIÓN =====
            ("msg.onboarding.welcome", "Bienvenida / primer contacto",
@"Hola {{customer_name}} 👋
Soy {{technician_name}} de {{shop_name}}.

Decime por favor:
1) Modelo exacto ({{device_brand}} {{device_model}})
2) Qué le pasa (síntoma)
3) Si tuvo golpe/humedad
4) Si es para *hoy* o *puede esperar*

Así te digo *precio estimado* y *turno*. ✅", true),

            ("order.status.received", "Equipo recibido",
@"Hola {{customer_name}} 👋
Recibimos tu {{device_brand}} {{device_model}} (Serial: {{device_serial}}).
Orden: {{order_code}} ✅

En breve hacemos diagnóstico y te pasamos presupuesto.

— {{shop_name}}", true),

            ("order.reception.checklist.request", "Checklist recepción (datos clave)",
@"Hola {{customer_name}} 👋
Para avanzar con la orden {{order_code}} confirmame:

• ¿Tenés el código/contraseña? (si aplica)
• ¿Está desactivado Buscar iPhone / Mi Cloud / FRP?
• ¿Querés *backup*? (puede demorar)

Así evitamos demoras. ✅", true),

            // ===== DIAGNÓSTICO / PRESUPUESTO =====
            ("order.diagnosis.started", "Diagnóstico en proceso",
@"Hola {{customer_name}} 👨‍🔧
Estamos revisando tu {{device_brand}} {{device_model}}.
Orden: {{order_code}}

Apenas tengamos diagnóstico te paso el presupuesto. ✅
— {{shop_name}}", true),

            ("order.quote.sent", "Presupuesto enviado",
@"Hola {{customer_name}} ✅
Presupuesto de tu {{device_brand}} {{device_model}} (Orden {{order_code}}):

• Total: {{quote_amount}} {{quote_currency}}
• Incluye: repuesto + mano de obra
• Garantía: {{warranty_days}} días

¿Confirmás para avanzar? Responde *SI* o *NO*.", true),

            ("order.quote.approved", "Presupuesto aprobado",
@"Genial {{customer_name}} ✅
Confirmado. Arrancamos con la reparación.

Orden: {{order_code}}
Te voy avisando avances. — {{shop_name}}", true),

            ("order.quote.rejected", "Presupuesto rechazado / devolución",
@"Hola {{customer_name}} 👋
Ok, no avanzamos con la reparación.

Orden: {{order_code}}
Podés retirar el equipo en {{pickup_address}} ({{pickup_hours}}).

— {{shop_name}}", true),

            // ===== ESTADOS =====
            ("order.status.inprogress", "En reparación",
@"Hola {{customer_name}} 👨‍🔧
Tu {{device_brand}} {{device_model}} está en reparación.
Orden: {{order_code}}

Cualquier novedad te aviso por acá. — {{shop_name}}", true),

            ("order.status.waiting_parts", "Esperando repuesto",
@"Hola {{customer_name}} 🧩
Tu reparación (Orden {{order_code}}) está *en espera de repuesto*.
Apenas ingrese lo instalamos y te aviso.

— {{shop_name}}", true),

            ("order.status.testing", "En pruebas",
@"Hola {{customer_name}} ✅
Tu {{device_brand}} {{device_model}} ya fue reparado y está en *pruebas*.
Orden: {{order_code}}

Si todo ok, queda listo para retirar. — {{shop_name}}", true),

            // ===== LISTO / PAGO / RETIRO =====
            ("order.status.ready", "Listo para retirar",
@"Hola {{customer_name}} ✅
Tu {{device_brand}} {{device_model}} ya está *listo*.
Orden: {{order_code}}

Total: {{quote_amount}} {{quote_currency}}
Pagado: {{paid_total}}
Saldo: {{balance_due}}

Retiro en: {{pickup_address}}
Horario: {{pickup_hours}}

— {{shop_name}}", true),

            ("order.payment.request", "Recordatorio de pago",
@"Hola {{customer_name}} 💳
Te paso el saldo de la orden {{order_code}}:

Saldo: {{balance_due}} {{quote_currency}}

Apenas se acredita te confirmo. — {{shop_name}}", true),

            ("order.status.delivered", "Equipo entregado",
@"Gracias {{customer_name}} 🙌
Entregado {{device_brand}} {{device_model}} (Orden {{order_code}}).

Garantía: {{warranty_days}} días.
Cualquier cosa escribime por acá.

— {{shop_name}}", true),

            // ===== GARANTÍA / POST-VENTA =====
            ("order.warranty.info", "Info de garantía",
@"Garantía {{shop_name}} ✅

• Duración: {{warranty_days}} días
• Cubre: falla del repuesto/instalación
• No cubre: golpes, humedad, manipulaciones, software/actualizaciones

Orden: {{order_code}}

— {{shop_name}}", true),

            // ===== FOLLOW-UP =====
            ("followup.no_response_24h", "Seguimiento 24hs sin respuesta",
@"Hola {{customer_name}} 👋
Te escribo por la orden {{order_code}}.
¿Confirmás si avanzamos con el presupuesto?

Si no respondés en 48hs dejamos la orden en pausa. — {{shop_name}}", true),
        };

        var keys = templates.Select(x => x.Key).ToArray();

        var existingKeys = await db.MessageTemplates
            .Where(x => x.ShopId == shopId && keys.Contains(x.Key))
            .Select(x => x.Key)
            .ToListAsync(ct);

        var existing = new HashSet<string>(existingKeys);

        var toAdd = new List<MessageTemplate>();
        foreach (var t in templates)
        {
            if (existing.Contains(t.Key)) continue;

            toAdd.Add(new MessageTemplate(
                shopId,
                t.Key,
                t.Title,
                t.Body,
                t.IsActive,
                now
            ));
        }

        if (toAdd.Count > 0)
            await db.MessageTemplates.AddRangeAsync(toAdd, ct);
    }

}
