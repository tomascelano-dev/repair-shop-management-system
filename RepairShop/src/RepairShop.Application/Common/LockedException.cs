namespace RepairShop.Application.Common;

/// <summary>
/// Used for temporary lockouts (e.g. repeated failed login attempts).
/// </summary>
public sealed class LockedException : RetryAfterException
{
    public LockedException(string message, int retryAfterSeconds) : base(message, retryAfterSeconds)
    {
    }
}
