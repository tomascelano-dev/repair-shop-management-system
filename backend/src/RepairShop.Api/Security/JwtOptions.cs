namespace RepairShop.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";

    /// <summary>Lifetime of the access token (kept in memory by the SPA).</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Lifetime of the refresh token (httpOnly cookie). Rotated on every use.</summary>
    public int RefreshTokenDays { get; set; } = 30;
}
