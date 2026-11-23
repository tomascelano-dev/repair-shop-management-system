using RepairShop.Domain.Common;

namespace RepairShop.Domain.Currency;

/// <summary>
/// Daily exchange rate (e.g. 1 USD = 1400.50 ARS). Source: "oficial", "blue", "manual"...
/// Rates fetched by the platform job are global (<see cref="OrganizationId"/> null); rates a shop types belong to its
/// organization and only affect its own reports.
/// </summary>
public sealed class ExchangeRate
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; private set; }
    public DateOnly Date { get; private set; }
    public string BaseCurrency { get; private set; } = null!;
    public string QuoteCurrency { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public decimal Rate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ExchangeRate() { }

    public ExchangeRate(DateOnly date, string baseCurrency, string quoteCurrency, string source, decimal rate, DateTime nowUtc, Guid? organizationId = null)
    {
        if (rate <= 0) throw new DomainException("La cotización debe ser mayor a 0.");
        OrganizationId = organizationId;
        Date = date;
        BaseCurrency = Money.NormalizeCurrency(baseCurrency);
        QuoteCurrency = Money.NormalizeCurrency(quoteCurrency);
        if (BaseCurrency == QuoteCurrency) throw new DomainException("Las monedas de la cotización deben ser distintas.");
        Source = NormalizeSource(source);
        Rate = decimal.Round(rate, 6);
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void UpdateRate(decimal rate, DateTime nowUtc)
    {
        if (rate <= 0) throw new DomainException("La cotización debe ser mayor a 0.");
        Rate = decimal.Round(rate, 6);
        UpdatedAtUtc = nowUtc;
    }

    public static string NormalizeSource(string? source)
    {
        var s = (source ?? "").Trim().ToLowerInvariant();
        if (s.Length is < 2 or > 30) throw new DomainException("Fuente de cotización inválida.");
        return s;
    }
}
