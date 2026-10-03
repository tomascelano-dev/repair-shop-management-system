using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RepairShop.Api.Common;
using RepairShop.Domain.Saas;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public sealed class PlanRequiredException(string message) : Exception(message);
public sealed class SubscriptionInactiveException(string message) : Exception(message);
public sealed class ForbiddenException(string message) : Exception(message);

public sealed record PlanModule(string Id, string Name);
public sealed record PlanInfo(string Id, string Name, string Description, decimal Price, string Currency, string[] Modules);

public sealed class SaasOptions
{
    public const string Section = "Saas";
    public int TrialDays { get; set; } = 14;
    // Days a past-due shop keeps full access before it becomes read-only.
    public int GraceDays { get; set; } = 7;
    public string PublicAppUrl { get; set; } = "http://127.0.0.1:5182";
    public bool SignupEnabled { get; set; } = true;
    public Dictionary<string, decimal> Prices { get; set; } = new() { ["Basic"] = 29900m, ["Standard"] = 44900m, ["Pro"] = 74900m };
    public string DataProtectionKeysPath { get; set; } = "";
}

public static class Plans
{
    public static readonly PlanModule[] Modules =
    [
        new("cash", "Caja y mostrador"),
        new("invoicing", "Facturación ARCA"),
        new("notifications", "Avisos por email"),
        new("sms", "Avisos por SMS"),
        new("agenda", "Agenda y turnos"),
        new("portal", "Portal, firma y seguimiento"),
        new("catalog", "Catálogo de servicios"),
        new("diagram", "Diagrama de daños"),
        new("surveys", "Encuestas de satisfacción"),
        new("technician", "App del técnico"),
        new("purchases", "Compras y precios"),
        new("stock", "Stock avanzado"),
        new("warranties", "Garantías y calidad"),
        new("profit", "Rentabilidad"),
        new("resale", "Reacondicionados"),
        new("business", "Empresas y sucursales"),
        new("api", "API, webhooks e integraciones"),
    ];

    private static readonly string[] BasicModules = ["cash", "invoicing", "notifications", "agenda", "portal", "catalog", "diagram"];
    private static readonly string[] StandardModules = [.. BasicModules, "sms", "surveys", "technician", "purchases", "stock", "warranties"];
    private static readonly string[] ProModules = Modules.Select(m => m.Id).ToArray();

    public static readonly string[] Ids = ["Basic", "Standard", "Pro"];

    public static PlanInfo Get(string id, SaasOptions options) => id switch
    {
        "Basic" => new("Basic", "Básico", "Todo el circuito del taller, caja, facturación y avisos.", options.Prices.GetValueOrDefault("Basic"), "ARS", BasicModules),
        "Standard" => new("Standard", "Estándar", "Suma stock avanzado, compras, garantías, encuestas y app del técnico.", options.Prices.GetValueOrDefault("Standard"), "ARS", StandardModules),
        "Pro" => new("Pro", "Profesional", "Todos los módulos: rentabilidad, reventa, empresas, sucursales y API.", options.Prices.GetValueOrDefault("Pro"), "ARS", ProModules),
        _ => throw new RepairShop.Domain.Common.DomainException("Plan inexistente."),
    };

    public static IEnumerable<PlanInfo> All(SaasOptions options) => Ids.Select(id => Get(id, options));

    public static string ModuleName(string id) => Modules.FirstOrDefault(m => m.Id == id)?.Name ?? id;

    // Maps an API path to the module that must be included in the plan. Null means core functionality.
    public static string? ModuleForPath(string path)
    {
        path = path.ToLowerInvariant();
        if (path.StartsWith("/api/v2/premium/"))
        {
            var rest = path["/api/v2/premium/".Length..];
            if (rest.StartsWith("workspace")) return null;
            if (rest.StartsWith("prices") || rest.StartsWith("stock/receive")) return "purchases";
            if (rest.StartsWith("stock")) return "stock";
            if (rest.StartsWith("expenses")) return "profit";
            if (rest.StartsWith("warranties")) return "warranties";
            if (rest.StartsWith("refurbs")) return "resale";
            if (rest.StartsWith("branches") || rest.StartsWith("contracts") || rest.StartsWith("equipment") || rest.StartsWith("business") || rest.StartsWith("settlements")) return "business";
            return null;
        }
        if (!path.StartsWith("/api/saas/")) return null;
        var saas = path["/api/saas/".Length..];
        string[] cash = ["cash", "sales", "accounts"];
        if (cash.Any(saas.StartsWith)) return "cash";
        if (saas.StartsWith("invoices") || saas.StartsWith("fiscal")) return "invoicing";
        if (saas.StartsWith("appointments")) return "agenda";
        if (saas.StartsWith("catalog")) return "catalog";
        if (saas.StartsWith("surveys")) return "surveys";
        if (saas.StartsWith("tech")) return "technician";
        if (saas.StartsWith("diagram")) return "diagram";
        if (saas.StartsWith("integrations")) return "api";
        return null;
    }
}

public sealed record SubscriptionState(Guid ShopId, string Plan, string Status, DateTime TrialEndsAtUtc, DateTime? PeriodEndsAtUtc, string[] Modules, bool ReadOnly, string? Reason);

public sealed class SubscriptionService(RepairShopDbContext db, IMemoryCache cache, Microsoft.Extensions.Options.IOptions<SaasOptions> options)
{
    private static string CacheKey(Guid shop) => $"saas:subscription:{shop}";
    public void Invalidate(Guid shop) => cache.Remove(CacheKey(shop));

    public async Task<ShopSubscription> EnsureAsync(Guid shop)
    {
        var current = await db.ShopSubscriptions.SingleOrDefaultAsync(x => x.ShopId == shop);
        if (current is not null) return current;
        var now = DateTime.UtcNow;
        current = new ShopSubscription { ShopId = shop, Plan = "Pro", Status = "Trialing", TrialEndsAtUtc = now.AddDays(options.Value.TrialDays), Price = Plans.Get("Pro", options.Value).Price };
        db.ShopSubscriptions.Add(current);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // Another request provisioned it first.
            db.Entry(current).State = EntityState.Detached;
            current = await db.ShopSubscriptions.SingleAsync(x => x.ShopId == shop);
        }
        return current;
    }

    public async Task<SubscriptionState> GetAsync(Guid shop)
    {
        if (cache.TryGetValue(CacheKey(shop), out SubscriptionState? cached) && cached is not null) return cached;
        var s = await EnsureAsync(shop);
        var state = Evaluate(s, options.Value, DateTime.UtcNow);
        cache.Set(CacheKey(shop), state, TimeSpan.FromSeconds(30));
        return state;
    }

    public static SubscriptionState Evaluate(ShopSubscription s, SaasOptions options, DateTime now)
    {
        var modules = Plans.Get(s.Plan, options).Modules;
        string? reason = s.Status switch
        {
            "Trialing" when now > s.TrialEndsAtUtc => "La prueba gratis terminó. Elegí un plan para seguir cargando datos.",
            "PastDue" when (s.CurrentPeriodEndsAtUtc ?? now).AddDays(options.GraceDays) < now => "No pudimos cobrar la suscripción. Actualizá el medio de pago.",
            "Cancelled" when (s.CurrentPeriodEndsAtUtc ?? now) < now => "La suscripción está cancelada. Reactivala para seguir cargando datos.",
            "Expired" => "La suscripción venció. Elegí un plan para seguir cargando datos.",
            _ => null,
        };
        return new(s.ShopId, s.Plan, s.Status, s.TrialEndsAtUtc, s.CurrentPeriodEndsAtUtc, modules, reason is not null, reason);
    }

    public async Task RequireModuleAsync(Guid shop, string module)
    {
        var state = await GetAsync(shop);
        if (!state.Modules.Contains(module)) throw new PlanRequiredException($"Tu plan no incluye {Plans.ModuleName(module)}. Mejorá tu plan para usarlo.");
    }
}

// Enforces plan modules and read-only mode for inactive subscriptions on every authenticated API call.
public sealed class SubscriptionGateMiddleware(RequestDelegate next)
{
    private static readonly string[] AlwaysWritable = ["/api/saas/billing", "/api/saas/profile", "/api/saas/onboarding", "/api/saas/me", "/api/v1/auth"];

    public async Task InvokeAsync(HttpContext context, SubscriptionService subscriptions)
    {
        var path = context.Request.Path.Value ?? "";
        var shop = CurrentUser.GetShopId(context.User);
        var gated = path.StartsWith("/api/v2/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/saas/", StringComparison.OrdinalIgnoreCase);
        if (!gated || shop == Guid.Empty || path.StartsWith("/api/v2/portal", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }
        var state = await subscriptions.GetAsync(shop);
        var module = Plans.ModuleForPath(path);
        if (module is not null && !state.Modules.Contains(module))
            throw new PlanRequiredException($"Tu plan no incluye {Plans.ModuleName(module)}. Mejorá tu plan para usarlo.");
        var write = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
        if (write && state.ReadOnly && !AlwaysWritable.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            throw new SubscriptionInactiveException(state.Reason!);
        await next(context);
    }
}
