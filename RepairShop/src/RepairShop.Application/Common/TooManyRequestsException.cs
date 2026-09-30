namespace RepairShop.Application.Common;

public sealed class TooManyRequestsException : RetryAfterException
{
    public TooManyRequestsException(string message, int retryAfterSeconds) : base(message, retryAfterSeconds)
    {
    }
}
