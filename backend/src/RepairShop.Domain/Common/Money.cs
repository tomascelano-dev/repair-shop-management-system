namespace RepairShop.Domain.Common;

public static class Money
{
    public static decimal Round(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Normalizes an ISO-4217 currency code (e.g. "ars" -> "ARS").
    /// </summary>
    public static string NormalizeCurrency(string? currency)
    {
        var c = (currency ?? "").Trim().ToUpperInvariant();
        if (c.Length != 3 || !c.All(char.IsLetter))
            throw new DomainException("La moneda debe ser un código ISO de 3 letras (ej: ARS, USD).");
        return c;
    }
}
