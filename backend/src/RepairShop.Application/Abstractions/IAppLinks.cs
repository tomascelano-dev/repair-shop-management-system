namespace RepairShop.Application.Abstractions;

/// <summary>Absolute URLs of the web app (frontend) used in messages, QR codes and emails.</summary>
public interface IAppLinks
{
    string Tracking(string orderToken);
    string Feedback(string orderToken);
    string Invitation(string token);
    string PasswordReset(string token);
    string Order(Guid orderId);
    string VerifyEmail(string token);

    /// <summary>Subscription page of the web app (return URL after paying).</summary>
    string Billing();

    /// <summary>Public base URL of the API (used for webhooks such as Mercado Pago notifications).</summary>
    string ApiBase { get; }
}
