using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;

namespace RepairShop.Api.Common;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public URL of the web app, e.g. https://app.mitaller.com (used in messages, QR codes and emails).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Public URL of this API, e.g. https://api.mitaller.com (used for webhooks).</summary>
    public string PublicApiUrl { get; set; } = "http://localhost:8080";
}

public sealed class AppLinks : IAppLinks
{
    private readonly string _web;
    private readonly string _api;

    public AppLinks(IOptions<AppOptions> options)
    {
        _web = options.Value.FrontendBaseUrl.TrimEnd('/');
        _api = options.Value.PublicApiUrl.TrimEnd('/');
    }

    public string Tracking(string orderToken) => $"{_web}/t/{Uri.EscapeDataString(orderToken)}";
    public string Feedback(string orderToken) => $"{_web}/t/{Uri.EscapeDataString(orderToken)}?encuesta=1";
    public string Invitation(string token) => $"{_web}/invitacion?token={Uri.EscapeDataString(token)}";
    public string PasswordReset(string token) => $"{_web}/restablecer?token={Uri.EscapeDataString(token)}";
    public string Order(Guid orderId) => $"{_web}/orders/{orderId}";
    public string VerifyEmail(string token) => $"{_web}/verificar-email?token={Uri.EscapeDataString(token)}";
    public string Billing() => $"{_web}/billing";
    public string ApiBase => _api;
}
