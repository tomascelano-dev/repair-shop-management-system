using RepairShop.Domain.Common;

namespace RepairShop.Domain.Customers;

public sealed class Customer : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    // Multi-sucursal
    public Guid ShopId { get; private set; }

    public string FullName { get; private set; } = null!;
    public string Phone { get; private set; } = null!;

    // Last 8 digits of the phone: used to detect duplicates regardless of formatting.
    public string PhoneKey { get; private set; } = "";

    public string? Email { get; private set; }

    // Fiscal data (optional, needed for invoices "A" or to identify the customer)
    public CustomerDocumentType DocumentType { get; private set; } = CustomerDocumentType.None;
    public string? DocumentNumber { get; private set; }
    public CustomerTaxCondition TaxCondition { get; private set; } = CustomerTaxCondition.ConsumidorFinal;
    public string? Address { get; private set; }

    public string? Notes { get; private set; }

    // Comma separated free tags (e.g. "vip,empresa").
    public string? Tags { get; private set; }

    // Consent: transactional messages (status updates) and marketing.
    public bool NotificationsOptIn { get; private set; } = true;
    public bool MarketingOptIn { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Customer() { } // EF

    public Customer(Guid shopId, string fullName, string phone, string? notes, DateTime nowUtc)
    {
        ShopId = shopId;
        SetBasics(fullName, phone, notes);
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    // Back-compat: constructor anterior sin ShopId.
    public Customer(string fullName, string phone, string? notes, DateTime nowUtc)
        : this(Guid.Empty, fullName, phone, notes, nowUtc)
    {
    }

    public void Update(string fullName, string phone, string? notes)
        => SetBasics(fullName, phone, notes);

    public void Update(string fullName, string phone, string? notes, DateTime nowUtc)
    {
        SetBasics(fullName, phone, notes);
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateContact(string? email, string? address, string? tags, DateTime nowUtc)
    {
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        if (email is not null && (!email.Contains('@') || email.Length > 180))
            throw new DomainException("El email del cliente es inválido.");

        Email = email;
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        Tags = NormalizeTags(tags);
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateFiscal(CustomerDocumentType documentType, string? documentNumber, CustomerTaxCondition taxCondition, DateTime nowUtc)
    {
        var digits = PhoneNumber.Digits(documentNumber);
        if (documentType == CustomerDocumentType.None)
        {
            digits = "";
        }
        else
        {
            if (digits.Length < 6) throw new DomainException("Falta el número de documento para el tipo seleccionado.");
            if (documentType is CustomerDocumentType.Cuit or CustomerDocumentType.Cuil && !IsValidCuit(digits))
                throw new DomainException("El CUIT/CUIL es inválido.");
        }

        DocumentType = documentType;
        DocumentNumber = digits.Length == 0 ? null : digits;
        TaxCondition = taxCondition;
        UpdatedAtUtc = nowUtc;
    }

    public void SetConsents(bool notificationsOptIn, bool marketingOptIn, DateTime nowUtc)
    {
        NotificationsOptIn = notificationsOptIn;
        MarketingOptIn = marketingOptIn;
        UpdatedAtUtc = nowUtc;
    }

    private void SetBasics(string fullName, string phone, string? notes)
    {
        FullName = (fullName ?? "").Trim();
        Phone = (phone ?? "").Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        PhoneKey = PhoneNumber.Key(Phone);

        if (FullName.Length < 3) throw new DomainException("El nombre del cliente es obligatorio (mín. 3 caracteres).");
        if (Phone.Length < 6) throw new DomainException("El teléfono del cliente es obligatorio (mín. 6 caracteres).");
    }

    private static string? NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) return null;
        var parts = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToArray();
        var joined = string.Join(",", parts);
        return joined.Length == 0 ? null : joined[..Math.Min(joined.Length, 300)];
    }

    /// <summary>
    /// Argentine CUIT/CUIL check digit (mod 11).
    /// </summary>
    public static bool IsValidCuit(string digits)
    {
        if (digits.Length != 11 || !digits.All(char.IsDigit)) return false;
        int[] weights = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
        var sum = 0;
        for (var i = 0; i < 10; i++) sum += (digits[i] - '0') * weights[i];
        var mod = 11 - (sum % 11);
        var check = mod == 11 ? 0 : mod == 10 ? 9 : mod;
        return check == digits[10] - '0';
    }
}

public enum CustomerDocumentType
{
    None = 0,
    Dni = 1,
    Cuit = 2,
    Cuil = 3,
    Passport = 4
}

public enum CustomerTaxCondition
{
    ConsumidorFinal = 0,
    ResponsableInscripto = 1,
    Monotributo = 2,
    Exento = 3
}
