using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Security;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Common;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;

namespace RepairShop.Application.Billing;

/// <summary>
/// Self-service signup from the website: creates the shop (its own organization), the owner as administrator
/// and a free trial, then signs the owner in. The email is confirmed afterwards with a link.
/// </summary>
public sealed class SignupService
{
    private readonly IShopRepository _shops;
    private readonly IUserRepository _users;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IShopProvisioner _provisioner;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLog _audit;
    private readonly AuthService _auth;
    private readonly IAdTrackingRepository _tracking;
    private readonly AdConversionService _conversions;
    private readonly BillingOptions _options;

    public SignupService(
        IShopRepository shops,
        IUserRepository users,
        ISubscriptionRepository subscriptions,
        IShopProvisioner provisioner,
        IPasswordHasher hasher,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IAuditLog audit,
        AuthService auth,
        IAdTrackingRepository tracking,
        AdConversionService conversions,
        IOptions<BillingOptions> options)
    {
        _shops = shops;
        _users = users;
        _subscriptions = subscriptions;
        _provisioner = provisioner;
        _hasher = hasher;
        _uow = uow;
        _clock = clock;
        _audit = audit;
        _auth = auth;
        _tracking = tracking;
        _conversions = conversions;
        _options = options.Value;
    }

    public async Task<AuthResult> SignupAsync(SignupRequest req, string? ip, string? userAgent, CancellationToken ct)
    {
        if (!_options.SignupEnabled) throw new ForbiddenException("El alta de talleres no está habilitada.");
        if (!req.AcceptTerms) throw new DomainException("Tenés que aceptar los Términos y la Política de privacidad.");

        var email = AppUser.NormalizeEmail(req.Email);
        if (email.Length is < 5 or > 180 || !IsPlausibleEmail(email)) throw new DomainException("Ingresá un email válido.");
        var shopName = (req.ShopName ?? "").Trim();
        if (shopName.Length is < 2 or > 120) throw new DomainException("Ingresá el nombre del taller (entre 2 y 120 caracteres).");
        var ownerName = (req.OwnerName ?? "").Trim();
        if (ownerName.Length is < 2 or > 120) throw new DomainException("Ingresá tu nombre (entre 2 y 120 caracteres).");
        PasswordPolicy.Validate(req.Password);
        var country = Subscription.NormalizeCountry(req.Country);

        if (await _users.GetByEmailAsync(email, ct) is not null)
            throw new ConflictException("Ya existe una cuenta con ese email. Iniciá sesión o recuperá tu contraseña.");

        var now = _clock.UtcNow;
        var regional = Regional.For(country, req.TimeZone);

        var shop = new Shop(shopName, phone: null, addressLine: null, city: null, country: country, nowUtc: now);
        shop.UpdateRegional(regional.Currency, regional.Currency, regional.PhoneCountryCode, regional.TimeZone, now);
        var user = new AppUser(shop.Id, email, ownerName, UserRole.Admin, _hasher.Hash(req.Password), now);
        var subscription = Subscription.StartTrial(shop.OrganizationId, country, _options.TrialDays, now);
        var attribution = new SignupAttribution(shop.OrganizationId, user.Id, ToData(req.Attribution), ip, userAgent, now);

        await _uow.InTransactionAsync(async tct =>
        {
            await _shops.AddAsync(shop, tct);
            await _uow.SaveChangesAsync(tct); // the shop must exist before users, templates and the subscription
            await _users.AddAsync(user, tct);
            await _subscriptions.AddAsync(subscription, tct);
            await _provisioner.ProvisionAsync(shop.Id, tct);
            await _tracking.AddAttributionAsync(attribution, tct);
            await _conversions.QueueTrialStartedAsync(attribution, user, country, tct);
            await _auth.QueueEmailVerificationAsync(user, tct);
            await _audit.AddAsync(shop.Id, "shop", shop.Id, "shop_signup", new Actor(user.Id, user.Email, user.Role.ToString()),
                new { country, trialDays = _options.TrialDays, source = attribution.Source, campaign = attribution.Campaign }, tct);
            await _uow.SaveChangesAsync(tct);
            return true;
        }, ct);

        return await _auth.SignInAsync(user, ip, userAgent, ct);
    }

    private static SignupAttributionData ToData(SignupAttributionRequest? a) => a is null
        ? new SignupAttributionData()
        : new SignupAttributionData(a.UtmSource, a.UtmMedium, a.UtmCampaign, a.UtmTerm, a.UtmContent, a.Gclid, a.Gbraid, a.Wbraid, a.Fbclid,
            a.Fbp, a.Fbc, a.LandingPath, a.Referrer, a.AdConsent, a.EventId);

    private static bool IsPlausibleEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 && at == email.LastIndexOf('@') && email.IndexOf('.', at) > at + 1 && !email.EndsWith('.') && !email.Any(char.IsWhiteSpace);
    }

    /// <summary>Sensible regional defaults per country; the shop can change them in Settings.</summary>
    internal sealed record Regional(string Currency, string PhoneCountryCode, string TimeZone)
    {
        private static readonly Dictionary<string, (string Phone, string Zone)> Countries = new()
        {
            ["AR"] = ("54", Shop.DefaultTimeZone),
            ["UY"] = ("598", "America/Montevideo"),
            ["PY"] = ("595", "America/Asuncion"),
            ["CL"] = ("56", "America/Santiago"),
            ["BO"] = ("591", "America/La_Paz"),
            ["PE"] = ("51", "America/Lima"),
            ["EC"] = ("593", "America/Guayaquil"),
            ["CO"] = ("57", "America/Bogota"),
            ["VE"] = ("58", "America/Caracas"),
            ["PA"] = ("507", "America/Panama"),
            ["CR"] = ("506", "America/Costa_Rica"),
            ["NI"] = ("505", "America/Managua"),
            ["HN"] = ("504", "America/Tegucigalpa"),
            ["SV"] = ("503", "America/El_Salvador"),
            ["GT"] = ("502", "America/Guatemala"),
            ["MX"] = ("52", "America/Mexico_City"),
            ["DO"] = ("1", "America/Santo_Domingo"),
            ["PR"] = ("1", "America/Puerto_Rico"),
            ["CU"] = ("53", "America/Havana"),
            ["BR"] = ("55", "America/Sao_Paulo"),
            ["US"] = ("1", "America/New_York"),
            ["CA"] = ("1", "America/Toronto"),
            ["ES"] = ("34", "Europe/Madrid"),
            ["PT"] = ("351", "Europe/Lisbon"),
            ["IT"] = ("39", "Europe/Rome"),
            ["FR"] = ("33", "Europe/Paris"),
            ["DE"] = ("49", "Europe/Berlin"),
            ["GB"] = ("44", "Europe/London"),
        };

        public static Regional For(string country, string? browserTimeZone)
        {
            var known = Countries.TryGetValue(country, out var c);
            var zone = IsValidTimeZone(browserTimeZone) ? browserTimeZone!.Trim() : known ? c.Zone : "UTC";
            return new Regional(SubscriptionService.CurrencyFor(country), known ? c.Phone : "1", zone);
        }

        private static bool IsValidTimeZone(string? id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return false;
            try { TimeZoneInfo.FindSystemTimeZoneById(id.Trim()); return true; }
            catch (TimeZoneNotFoundException) { return false; }
            catch (InvalidTimeZoneException) { return false; }
        }
    }
}
