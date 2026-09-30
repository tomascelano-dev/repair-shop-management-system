namespace RepairShop.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";
}
