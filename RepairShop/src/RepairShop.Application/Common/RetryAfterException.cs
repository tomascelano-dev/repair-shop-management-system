namespace RepairShop.Application.Common;

public abstract class RetryAfterException : Exception
{
    protected RetryAfterException(string message, int retryAfterSeconds) : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int RetryAfterSeconds { get; }
}
