using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RepairShop.Api.Common;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Security;
using RepairShop.Domain.Users;

namespace RepairShop.Api.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _opt;
    private readonly IDateTimeProvider _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtOptions> opt, IDateTimeProvider clock)
    {
        _opt = opt.Value;
        _clock = clock;
        _credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)), SecurityAlgorithms.HmacSha256);
    }

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(Math.Clamp(_opt.RefreshTokenDays, 1, 365));

    public string CreateToken(AppUser user) => CreateAccessToken(user, user.ShopId, user.Role, Guid.Empty).Token;

    public AccessToken CreateAccessToken(AppUser user, Guid shopId, UserRole role, Guid organizationId)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(Math.Clamp(_opt.AccessTokenMinutes, 1, 24 * 60));

        var claims = new List<Claim>
        {
            new(ClaimNames.UserId, user.Id.ToString()),
            new(ClaimNames.ShopId, shopId.ToString()),
            new(ClaimNames.OrganizationId, (organizationId == Guid.Empty ? shopId : organizationId).ToString()),
            new(ClaimNames.Email, user.Email),
            new(ClaimNames.Name, user.DisplayName),
            new(ClaimNames.Role, role.ToString()),
            new(ClaimNames.SecurityStamp, user.SecurityStamp),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var token = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims, notBefore: now.AddSeconds(-5), expires: expires, signingCredentials: _credentials);
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
