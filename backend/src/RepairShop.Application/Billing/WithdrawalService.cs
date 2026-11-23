using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Common;
using RepairShop.Domain.Users;

namespace RepairShop.Application.Billing;

public sealed record WithdrawalRequestBody(string? Email, string? Name, string? Reason);

/// <summary>Identification code of the request (shown to the requester right away) and when it was filed.</summary>
public sealed record WithdrawalResponse(string Code, DateTime CreatedAtUtc);

public interface IWithdrawalRepository
{
    Task AddAsync(WithdrawalRequest request, CancellationToken ct);
}

/// <summary>Plain emails from the platform itself (not on behalf of a shop): legal notices to customers and the owner.</summary>
public interface IPlatformMailer
{
    /// <summary>False when email is not configured or sending failed (already logged).</summary>
    Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct);
}

/// <summary>
/// "Botón de arrepentimiento": records the request with an identification code, confirms it by email to account
/// holders and notifies the platform owner (CONTACT_EMAIL), who cancels and refunds by hand.
/// </summary>
public sealed class WithdrawalService
{
    private readonly IWithdrawalRepository _requests;
    private readonly IUserRepository _users;
    private readonly IShopRepository _shops;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly IPlatformMailer _mailer;
    private readonly BillingOptions _options;
    private readonly ILogger<WithdrawalService> _logger;

    public WithdrawalService(IWithdrawalRepository requests, IUserRepository users, IShopRepository shops, ISubscriptionRepository subscriptions,
        IUnitOfWork uow, IDateTimeProvider clock, IPlatformMailer mailer, IOptions<BillingOptions> options, ILogger<WithdrawalService> logger)
    {
        _requests = requests;
        _users = users;
        _shops = shops;
        _subscriptions = subscriptions;
        _uow = uow;
        _clock = clock;
        _mailer = mailer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WithdrawalResponse> RequestAsync(WithdrawalRequestBody body, string? clientIp, CancellationToken ct)
    {
        var email = AppUser.NormalizeEmail(body.Email);
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || email.IndexOf('.', at) < at + 2 || email.Any(char.IsWhiteSpace))
            throw new DomainException("Ingresá el email de tu cuenta.");

        var user = await _users.GetByEmailAsync(email, ct);
        var shop = user is null ? null : await _shops.GetByIdAsync(user.ShopId, ct);
        var subscription = shop is null ? null : await _subscriptions.GetByOrganizationAsync(shop.OrganizationId, ct);

        var request = new WithdrawalRequest(email, body.Name ?? "", body.Reason, shop?.OrganizationId, clientIp, _clock.UtcNow);
        await _requests.AddAsync(request, ct);
        await _uow.SaveChangesAsync(ct);
        _logger.LogWarning("Withdrawal request {Code} filed for {Organization}.", request.Code, shop?.OrganizationId.ToString() ?? "(no account)");

        var when = request.CreatedAtUtc.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";
        // Only account holders get the confirmation: an anonymous form must not send mail to arbitrary addresses.
        if (user is not null)
        {
            var confirmation =
                // Security audit 2026-11-23 (M-08): the form is anonymous, so nothing typed in it (the name) goes into a mail the
                // platform sends to the account holder: it could carry a phishing text from a legitimate sender.
                $"Hola {user.DisplayName},\n\n" +
                $"Recibimos tu solicitud de arrepentimiento (revocación de la contratación de RepairShop).\n\n" +
                $"Código de identificación: {request.Code}\nFecha: {when}\n\n" +
                "La procesamos sin costo: cancelamos la suscripción y, si hubo un pago, te lo reintegramos por el mismo medio " +
                "(Mercado Pago). Te escribimos a este email cuando esté hecho.\n\n" +
                "Si no hiciste esta solicitud, respondé este mensaje: no cancelamos nada sin confirmarlo con vos desde este email.";
            await _mailer.SendAsync(email, $"Solicitud de arrepentimiento {request.Code}", confirmation, ct);
        }

        if (!string.IsNullOrWhiteSpace(_options.ContactEmail))
        {
            var account = shop is null
                ? "No hay una cuenta con ese email."
                : $"Taller: {shop.Name} (organización {shop.OrganizationId})\n" +
                  $"Suscripción: {subscription?.Provider} · {subscription?.Status} · plan {subscription?.Plan} · " +
                  $"{subscription?.Amount?.ToString(CultureInfo.InvariantCulture) ?? "-"} {subscription?.Currency} · " +
                  $"id en el proveedor {subscription?.ProviderSubscriptionId ?? "-"}";
            var notice =
                $"Nueva solicitud de arrepentimiento (Res. 424/2020).\n\n" +
                $"Código: {request.Code}\nFecha: {when}\nNombre: {request.Name}\nEmail: {email}\nMotivo: {request.Reason ?? "(sin motivo)"}\n\n" +
                $"{account}\n\n" +
                "Qué hacer: cancelar la suscripción en Mercado Pago (o desde la app con esa cuenta), reintegrar el pago si lo hubo " +
                "y avisarle a la persona.";
            await _mailer.SendAsync(_options.ContactEmail.Trim(), $"[RepairShop] Arrepentimiento {request.Code}", notice, ct);
        }
        else
        {
            _logger.LogError("Withdrawal request {Code}: CONTACT_EMAIL is not set, nobody was notified.", request.Code);
        }

        return new WithdrawalResponse(request.Code, request.CreatedAtUtc);
    }
}
