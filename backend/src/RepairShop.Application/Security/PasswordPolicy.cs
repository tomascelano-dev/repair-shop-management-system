using RepairShop.Domain.Common;

namespace RepairShop.Application.Security;

public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static void Validate(string? password)
    {
        password ??= "";
        if (password.Length < MinLength) throw new DomainException($"La contraseña debe tener al menos {MinLength} caracteres.");
        if (password.Length > 128) throw new DomainException("La contraseña es demasiado larga.");
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new DomainException("La contraseña debe combinar letras y números.");
    }
}
