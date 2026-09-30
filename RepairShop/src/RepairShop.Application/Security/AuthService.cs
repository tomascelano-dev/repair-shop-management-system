using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;

namespace RepairShop.Application.Security;

public sealed class AuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public AuthService(IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, ct);

        if (user is null || !_hasher.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials.");

        var token = _jwt.CreateToken(user);

        return new LoginResponse(
            AccessToken: token,
            User: new UserResponse(user.Id, user.ShopId, user.Email, user.DisplayName, user.Role.ToString())
        );
    }
}
