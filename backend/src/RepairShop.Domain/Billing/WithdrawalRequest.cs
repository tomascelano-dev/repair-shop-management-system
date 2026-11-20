using System.Security.Cryptography;
using RepairShop.Domain.Common;

namespace RepairShop.Domain.Billing;

/// <summary>
/// A request to withdraw from the contract ("botón de arrepentimiento", Argentina's Res. 424/2020): anyone can file
/// it from the website without signing in and gets an identification code right away. The platform owner cancels
/// the subscription and refunds by hand from the notice it receives.
/// </summary>
public sealed class WithdrawalRequest
{
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I/L

    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Identification code given to the requester (ARR-XXXXXXXX).</summary>
    public string Code { get; private set; } = "";

    public string Email { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Reason { get; private set; }

    /// <summary>Organization of the account with that email, when there is one.</summary>
    public Guid? OrganizationId { get; private set; }

    public string? ClientIp { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private WithdrawalRequest() { } // EF

    public WithdrawalRequest(string email, string name, string? reason, Guid? organizationId, string? clientIp, DateTime nowUtc)
    {
        Email = (email ?? "").Trim().ToLowerInvariant();
        Name = (name ?? "").Trim();
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (Email.Length is < 5 or > 180) throw new DomainException("Ingresá un email válido.");
        if (Name.Length is < 2 or > 120) throw new DomainException("Ingresá tu nombre (entre 2 y 120 caracteres).");
        if (Reason is { Length: > 1000 }) throw new DomainException("El motivo puede tener hasta 1000 caracteres.");
        OrganizationId = organizationId;
        ClientIp = string.IsNullOrWhiteSpace(clientIp) ? null : clientIp.Trim()[..Math.Min(clientIp.Trim().Length, 64)];
        CreatedAtUtc = nowUtc;
        Code = NewCode();
    }

    public static string NewCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++) chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return "ARR-" + new string(chars);
    }
}
