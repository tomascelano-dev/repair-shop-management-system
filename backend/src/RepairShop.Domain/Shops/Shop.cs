using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Notifications;

namespace RepairShop.Domain.Shops;

public sealed class Shop
{
    public const string DefaultTimeZone = "America/Argentina/Buenos_Aires";

    public Guid Id { get; private set; } = Guid.NewGuid();

    // Branches of the same owner share the organization id (multi-sucursal).
    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Phone { get; private set; }
    public string? AddressLine { get; private set; }
    public string? City { get; private set; }
    public string? Country { get; private set; }

    public bool IsActive { get; private set; } = true;

    // ===== Settings =====
    public string? LegalName { get; private set; }
    public string? TaxId { get; private set; }               // CUIT
    public CustomerTaxCondition TaxCondition { get; private set; } = CustomerTaxCondition.Monotributo;
    public string? Email { get; private set; }
    public Guid? LogoFileId { get; private set; }

    public string DefaultCurrency { get; private set; } = "ARS";
    public string ReportingCurrency { get; private set; } = "ARS";
    public string PhoneCountryCode { get; private set; } = "54";
    public string TimeZone { get; private set; } = DefaultTimeZone;

    public int DefaultWarrantyDays { get; private set; } = 90;
    public int QuoteValidityDays { get; private set; } = 7;

    // Legal text printed on the intake receipt (terms & conditions).
    public string? ReceptionTerms { get; private set; }
    public string? WarrantyTerms { get; private set; }
    public string? PickupHours { get; private set; }
    public string? GoogleReviewUrl { get; private set; }

    // Days after "Ready" when a pickup reminder is sent (e.g. "7,15,30").
    public string ReadyReminderDays { get; private set; } = "7,15,30";
    // Days without status changes before an open order is flagged as stale.
    public int StaleOrderDays { get; private set; } = 5;

    public bool NotificationsEnabled { get; private set; } = true;
    public NotificationChannel DefaultNotificationChannel { get; private set; } = NotificationChannel.WhatsApp;
    public bool SendFeedbackSurvey { get; private set; } = true;
    public bool RequireOpenCashSession { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Shop() { } // EF

    public Shop(
        string name,
        string? phone,
        string? addressLine,
        string? city,
        string? country,
        DateTime nowUtc)
        : this(Guid.Empty, name, phone, addressLine, city, country, nowUtc)
    {
    }

    public Shop(
        Guid organizationId,
        string name,
        string? phone,
        string? addressLine,
        string? city,
        string? country,
        DateTime nowUtc)
    {
        // A standalone shop is its own organization.
        OrganizationId = organizationId == Guid.Empty ? Id : organizationId;
        SetValues(name, phone, addressLine, city, country);
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(
        string name,
        string? phone,
        string? addressLine,
        string? city,
        string? country,
        DateTime nowUtc)
    {
        SetValues(name, phone, addressLine, city, country);
        UpdatedAtUtc = nowUtc;
    }

    // Backward-compatible convenience overload (older code may pass name/address/phone)
    public void Update(string name, string? addressLine, string? phone)
        => SetValues(name, phone, addressLine, City, Country);

    public void SetActive(bool isActive, DateTime nowUtc)
    {
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }

    // Backward-compatible convenience overload
    public void SetActive(bool isActive) => IsActive = isActive;

    public void UpdateBusiness(string? legalName, string? taxId, CustomerTaxCondition taxCondition, string? email, DateTime nowUtc)
    {
        var digits = PhoneNumber.Digits(taxId);
        if (digits.Length > 0 && !Customer.IsValidCuit(digits))
            throw new DomainException("El CUIT de la sucursal es inválido.");

        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        if (email is not null && !email.Contains('@')) throw new DomainException("El email de la sucursal es inválido.");

        LegalName = Clean(legalName, 160);
        TaxId = digits.Length == 0 ? null : digits;
        TaxCondition = taxCondition;
        Email = email;
        UpdatedAtUtc = nowUtc;
    }

    public void SetLogo(Guid? fileId, DateTime nowUtc)
    {
        LogoFileId = fileId;
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateRegional(string defaultCurrency, string reportingCurrency, string phoneCountryCode, string timeZone, DateTime nowUtc)
    {
        DefaultCurrency = Money.NormalizeCurrency(defaultCurrency);
        ReportingCurrency = Money.NormalizeCurrency(reportingCurrency);

        var cc = PhoneNumber.Digits(phoneCountryCode);
        if (cc.Length is < 1 or > 4) throw new DomainException("El código de país es inválido.");
        PhoneCountryCode = cc;

        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? DefaultTimeZone : timeZone.Trim();
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateOperations(
        int defaultWarrantyDays,
        int quoteValidityDays,
        string? receptionTerms,
        string? warrantyTerms,
        string? pickupHours,
        string? googleReviewUrl,
        string? readyReminderDays,
        int staleOrderDays,
        bool requireOpenCashSession,
        DateTime nowUtc)
    {
        if (defaultWarrantyDays is < 0 or > 3650) throw new DomainException("La garantía debe estar entre 0 y 3650 días.");
        if (quoteValidityDays is < 1 or > 365) throw new DomainException("La validez del presupuesto debe estar entre 1 y 365 días.");
        if (staleOrderDays is < 1 or > 365) throw new DomainException("Los días para considerar una orden estancada deben estar entre 1 y 365.");

        googleReviewUrl = Clean(googleReviewUrl, 500);
        if (googleReviewUrl is not null && !googleReviewUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("El link de reseñas de Google debe empezar con https://");

        DefaultWarrantyDays = defaultWarrantyDays;
        QuoteValidityDays = quoteValidityDays;
        ReceptionTerms = Clean(receptionTerms, 4000);
        WarrantyTerms = Clean(warrantyTerms, 4000);
        PickupHours = Clean(pickupHours, 200);
        GoogleReviewUrl = googleReviewUrl;
        ReadyReminderDays = NormalizeReminderDays(readyReminderDays);
        StaleOrderDays = staleOrderDays;
        RequireOpenCashSession = requireOpenCashSession;
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateNotifications(bool enabled, NotificationChannel defaultChannel, bool sendFeedbackSurvey, DateTime nowUtc)
    {
        NotificationsEnabled = enabled;
        DefaultNotificationChannel = defaultChannel;
        SendFeedbackSurvey = sendFeedbackSurvey;
        UpdatedAtUtc = nowUtc;
    }

    public IReadOnlyList<int> GetReadyReminderDays()
        => ReadyReminderDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var d) ? d : 0)
            .Where(d => d > 0)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

    private static string NormalizeReminderDays(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var days = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var d) ? d : -1)
            .ToList();
        if (days.Any(d => d is < 1 or > 365)) throw new DomainException("Los días de recordatorio deben ser números entre 1 y 365 (ej: 7,15,30).");
        return string.Join(",", days.Distinct().OrderBy(d => d));
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > max) throw new DomainException($"El valor es demasiado largo (máx. {max} caracteres).");
        return value;
    }

    private void SetValues(string name, string? phone, string? addressLine, string? city, string? country)
    {
        Name = (name ?? "").Trim();
        Phone = phone?.Trim();
        AddressLine = addressLine?.Trim();
        City = city?.Trim();
        Country = country?.Trim();

        if (Name.Length < 2)
            throw new DomainException("El nombre de la sucursal es obligatorio (mín. 2 caracteres).");
    }
}
