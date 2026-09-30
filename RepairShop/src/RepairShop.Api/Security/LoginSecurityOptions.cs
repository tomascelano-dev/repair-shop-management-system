namespace RepairShop.Api.Security;

public sealed class LoginSecurityOptions
{
    public const string SectionName = "Security:Login";

    public RateLimitOptions RateLimit { get; init; } = new();
    public LockoutOptions Lockout { get; init; } = new();

    public sealed class RateLimitOptions
    {
        /// <summary>
        /// Maximum number of login requests per window per IP.
        /// </summary>
        public int PermitLimit { get; init; } = 10;

        /// <summary>
        /// Window size in seconds.
        /// </summary>
        public int WindowSeconds { get; init; } = 60;
    }

    public sealed class LockoutOptions
    {
        /// <summary>
        /// Number of failed attempts before locking the identifier.
        /// </summary>
        public int MaxFailedAttempts { get; init; } = 5;

        /// <summary>
        /// Cooldown time in seconds.
        /// </summary>
        public int CooldownSeconds { get; init; } = 600;

        /// <summary>
        /// After this idle period (seconds) the failure counter resets.
        /// </summary>
        public int FailureResetSeconds { get; init; } = 900;
    }
}
