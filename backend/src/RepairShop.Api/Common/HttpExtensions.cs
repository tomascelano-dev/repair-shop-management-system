using RepairShop.Application.Common;

namespace RepairShop.Api.Common;

public static class HttpExtensions
{
    public const string TotalCountHeader = "X-Total-Count";

    public static string? ClientIp(this HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    public static string? UserAgent(this HttpContext http)
    {
        var ua = http.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? null : ua.Length > 300 ? ua[..300] : ua;
    }

    public static IReadOnlyList<T> WithTotal<T>(this HttpResponse response, PagedResult<T> page)
    {
        response.Headers[TotalCountHeader] = page.Total.ToString();
        return page.Items;
    }
}
