using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using RepairShop.Api.Common;
using RepairShop.Application.Billing;
using RepairShop.Application.Common;
using RepairShop.Domain.Billing;

namespace RepairShop.Api.Billing;

/// <summary>The action needs a module that only some plans include.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresModuleAttribute : Attribute
{
    public RequiresModuleAttribute(string module) => Module = module;
    public string Module { get; }
}

/// <summary>Works whatever the subscription state (sign in, billing, profile).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipSubscriptionGateAttribute : Attribute;

/// <summary>
/// Enforces the subscription on every staff request: without full access (trial over, unpaid) the account is
/// read-only, and modules outside the plan answer 402 with the module name so the app can offer an upgrade.
/// </summary>
public sealed class SubscriptionGateFilter : IAsyncActionFilter
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);

    private readonly SubscriptionService _subscriptions;
    private readonly IMemoryCache _cache;

    public SubscriptionGateFilter(SubscriptionService subscriptions, IMemoryCache cache)
    {
        _subscriptions = subscriptions;
        _cache = cache;
    }

    public static string CacheKey(Guid organizationId) => $"subscription-access:{organizationId:N}";

    public static void Evict(IMemoryCache cache, Guid organizationId) => cache.Remove(CacheKey(organizationId));

    public static SubscriptionAccess? Current(HttpContext http) => http.Items[typeof(SubscriptionAccess)] as SubscriptionAccess;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var metadata = context.ActionDescriptor.EndpointMetadata;
        var http = context.HttpContext;
        var organizationId = CurrentUser.GetOrganizationId(http.User);
        if (metadata.OfType<IAllowAnonymous>().Any() || metadata.OfType<SkipSubscriptionGateAttribute>().Any()
            || http.User.Identity?.IsAuthenticated != true || organizationId == Guid.Empty)
        {
            await next();
            return;
        }

        var access = await _cache.GetOrCreateAsync(CacheKey(organizationId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return _subscriptions.GetAccessAsync(organizationId, http.RequestAborted);
        });
        http.Items[typeof(SubscriptionAccess)] = access;

        foreach (var module in metadata.OfType<RequiresModuleAttribute>().Select(a => a.Module).Distinct())
            EnsureModule(access!, module);

        if (!access!.HasFullAccess && !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
        {
            throw new PaymentRequiredException(PaymentRequiredException.SubscriptionInactive,
                "Tu cuenta está en modo consulta porque la prueba gratis o la suscripción terminó. Elegí un plan para seguir cargando datos.");
        }

        await next();
    }

    public static void EnsureModule(SubscriptionAccess access, string module)
    {
        if (access.Includes(module)) return;
        throw new PaymentRequiredException(PaymentRequiredException.PlanUpgradeRequired,
            $"Tu plan {access.Plan.Name} no incluye {ModuleLabel(module)}. Pasate a un plan superior para usarlo.", module);
    }

    public static string ModuleLabel(string module) => module switch
    {
        PlanModules.Purchasing => "compras y proveedores",
        PlanModules.Reports => "los reportes detallados",
        PlanModules.Transfers => "las transferencias entre sucursales",
        PlanModules.Audit => "la auditoría",
        PlanModules.Ai => "las sugerencias con IA",
        _ => module
    };
}
