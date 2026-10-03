namespace RepairShop.Application.Abstractions;

/// <summary>
/// Symmetric encryption for secrets at rest (unlock codes, integration credentials).
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}
