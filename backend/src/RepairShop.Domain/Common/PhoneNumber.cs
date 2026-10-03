namespace RepairShop.Domain.Common;

public static class PhoneNumber
{
    /// <summary>
    /// Digits only (keeps the number comparable regardless of formatting).
    /// </summary>
    public static string Digits(string? phone)
        => new((phone ?? "").Where(char.IsDigit).ToArray());

    /// <summary>
    /// Stable key used to detect duplicates across formats
    /// ("011 15 2345-6789", "+54 9 11 2345 6789" => "23456789").
    /// </summary>
    public static string Key(string? phone)
    {
        var digits = Digits(phone);
        return digits.Length <= 8 ? digits : digits[^8..];
    }

    /// <summary>
    /// Best-effort E.164 digits (without '+') suitable for WhatsApp links (wa.me).
    /// Includes Argentina's mobile rules: country code 54 + '9' + area code + number,
    /// dropping the trunk '0' and the legacy '15' mobile prefix when detectable.
    /// </summary>
    public static string ToWhatsAppDigits(string? phone, string defaultCountryCode = "54")
    {
        var raw = (phone ?? "").Trim();
        var digits = Digits(raw);
        if (digits.Length == 0) return "";

        var cc = Digits(defaultCountryCode);
        if (cc.Length == 0) cc = "54";

        if (raw.StartsWith("+") || digits.StartsWith("00"))
        {
            digits = digits.StartsWith("00") ? digits[2..] : digits;
            return cc == "54" && digits.StartsWith("54") ? NormalizeArgentina(digits[2..]) : digits;
        }

        if (digits.StartsWith(cc) && digits.Length > 10)
        {
            return cc == "54" ? NormalizeArgentina(digits[2..]) : digits;
        }

        return cc == "54" ? NormalizeArgentina(digits) : cc + digits.TrimStart('0');
    }

    private static string NormalizeArgentina(string national)
    {
        // national may start with 9 (mobile marker), 0 (trunk) and may contain "15" after the area code.
        if (national.StartsWith("9") && national.Length == 11) return "54" + national;

        national = national.TrimStart('0');

        // 11 15 2345 6789 -> 11 2345 6789 (area code lengths 2-4)
        if (national.Length == 12)
        {
            foreach (var areaLen in new[] { 2, 3, 4 })
            {
                if (national.Length > areaLen + 2 && national.Substring(areaLen, 2) == "15")
                {
                    national = national[..areaLen] + national[(areaLen + 2)..];
                    break;
                }
            }
        }

        if (national.Length == 10) return "549" + national;
        // Unknown shape: keep digits with country code so the link still works most of the time.
        return "54" + national;
    }
}
