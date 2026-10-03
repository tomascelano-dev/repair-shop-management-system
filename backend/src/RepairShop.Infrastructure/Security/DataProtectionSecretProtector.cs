using Microsoft.AspNetCore.DataProtection;
using RepairShop.Application.Abstractions;

namespace RepairShop.Infrastructure.Security;

/// <summary>
/// Encrypts secrets at rest with ASP.NET Core Data Protection (keys persisted in the database,
/// so every instance of the API can decrypt).
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("RepairShop.Secrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext ?? "");

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
