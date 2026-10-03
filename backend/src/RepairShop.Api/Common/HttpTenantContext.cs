using RepairShop.Application.Abstractions;

namespace RepairShop.Api.Common;

/// <summary>Current shop from the access token (null for anonymous requests and background work).</summary>
public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _http;

    public HttpTenantContext(IHttpContextAccessor http) => _http = http;

    public Guid? ShopId
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;
            var id = CurrentUser.GetShopId(user);
            return id == Guid.Empty ? null : id;
        }
    }
}
