using Microsoft.Extensions.Logging;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Common;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;

namespace RepairShop.Application.Security;

public sealed class AuthService
{
    // Concurrent refreshes (two tabs) within this window are not treated as token theft.
    private static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(30);

    private readonly IUserRepository _users;
    private readonly IShopRepository _shops;
    private readonly IUserShopAccessRepository _access;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly INotificationOutboxRepository _outbox;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly IAppLinks _links;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IShopRepository shops,
        IUserShopAccessRepository access,
        IRefreshTokenRepository refreshTokens,
        INotificationOutboxRepository outbox,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IAppLinks links,
        ILogger<AuthService> logger)
    {
        _users = users;
        _shops = shops;
        _access = access;
        _refreshTokens = refreshTokens;
        _outbox = outbox;
        _hasher = hasher;
        _jwt = jwt;
        _uow = uow;
        _clock = clock;
        _links = links;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest req, string? ip, string? userAgent, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(req.Email, ct);
        if (user is null || !user.IsActive || user.HasPendingInvitation || !_hasher.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedException("Email o contraseña incorrectos.");

        var shops = await GetAccessibleShopsAsync(user, ct);
        var target = shops.FirstOrDefault(s => s.IsHome) ?? shops.FirstOrDefault()
                     ?? throw new UnauthorizedException("Tu usuario no tiene sucursales activas.");

        user.RegisterLogin(_clock.UtcNow);
        return await IssueAsync(user, target, shops, Guid.Empty, ip, userAgent, ct);
    }

    public async Task<AuthResult> RefreshAsync(string? rawToken, string? ip, string? userAgent, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var token = await FindTokenAsync(rawToken, ct);

        if (token.RevokedAtUtc is not null)
        {
            var benignRace = token.ReplacedByTokenId is not null && token.RevokedAtUtc > now - RotationGracePeriod;
            if (!benignRace)
            {
                // A rotated token was reused: assume theft and kill the whole session family.
                if (token.ReplacedByTokenId is not null) await RevokeFamilyAsync(token, "reuse_detected", now, ct);
                await _uow.SaveChangesAsync(ct);
                throw new UnauthorizedException("La sesión expiró. Volvé a iniciar sesión.");
            }
        }
        else if (token.ExpiresAtUtc <= now)
        {
            throw new UnauthorizedException("La sesión expiró. Volvé a iniciar sesión.");
        }

        var user = await _users.GetByIdAsync(token.UserId, ct);
        if (user is null || !user.IsActive) throw new UnauthorizedException("La sesión expiró. Volvé a iniciar sesión.");

        var shops = await GetAccessibleShopsAsync(user, ct);
        var target = shops.FirstOrDefault(s => s.ShopId == token.ShopId) ?? shops.FirstOrDefault(s => s.IsHome)
                     ?? throw new UnauthorizedException("Tu usuario no tiene sucursales activas.");

        var result = await IssueAsync(user, target, shops, token.FamilyId, ip, userAgent, ct, rotate: token);
        return result;
    }

    public async Task LogoutAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;
        var token = await _refreshTokens.GetByHashAsync(TokenHasher.Hash(rawToken), ct);
        if (token is null) return;
        await RevokeFamilyAsync(token, "logout", _clock.UtcNow, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task LogoutEverywhereAsync(Guid userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await _users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");
        foreach (var t in await _refreshTokens.ListActiveByUserAsync(userId, now, ct)) t.Revoke("logout_all", now);
        user.RotateSecurityStamp(now);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<AuthResult> SwitchShopAsync(Guid userId, string? rawToken, Guid targetShopId, string? ip, string? userAgent, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive) throw new UnauthorizedException("Sesión inválida.");

        var shops = await GetAccessibleShopsAsync(user, ct);
        var target = shops.FirstOrDefault(s => s.ShopId == targetShopId)
                     ?? throw new ForbiddenException("No tenés acceso a esa sucursal.");

        RefreshToken? current = null;
        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            current = await _refreshTokens.GetByHashAsync(TokenHasher.Hash(rawToken), ct);
            if (current is not null && current.UserId != userId) current = null;
        }

        return await IssueAsync(user, target, shops, current?.FamilyId ?? Guid.Empty, ip, userAgent, ct, rotate: current);
    }

    public async Task<MeResponse> MeAsync(Guid userId, Guid shopId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct) ?? throw new UnauthorizedException("Sesión inválida.");
        var shops = await GetAccessibleShopsAsync(user, ct);
        var current = shops.FirstOrDefault(s => s.ShopId == shopId) ?? throw new ForbiddenException("No tenés acceso a esa sucursal.");
        var shop = await _shops.GetByIdAsync(shopId, ct);
        return new MeResponse(
            ToUserResponse(user, current, shop),
            shops.Select(s => s.ToResponse()).ToList(),
            Permissions.For(current.Role));
    }

    public async Task<AuthResult> ChangePasswordAsync(Guid userId, Guid shopId, ChangePasswordRequest req, string? ip, string? userAgent, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await _users.GetByIdAsync(userId, ct) ?? throw new UnauthorizedException("Sesión inválida.");
        if (!_hasher.Verify(req.CurrentPassword, user.PasswordHash)) throw new DomainException("La contraseña actual no es correcta.");
        if (req.CurrentPassword == req.NewPassword) throw new DomainException("La nueva contraseña debe ser distinta de la actual.");
        PasswordPolicy.Validate(req.NewPassword);

        user.ChangePassword(_hasher.Hash(req.NewPassword), now);
        foreach (var t in await _refreshTokens.ListActiveByUserAsync(userId, now, ct)) t.Revoke("password_changed", now);

        var shops = await GetAccessibleShopsAsync(user, ct);
        var target = shops.FirstOrDefault(s => s.ShopId == shopId) ?? shops.First(s => s.IsHome);
        return await IssueAsync(user, target, shops, Guid.Empty, ip, userAgent, ct);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await _users.GetByEmailAsync(email, ct);
        if (user is null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for unknown/inactive email.");
            return; // never reveal whether the email exists
        }

        var raw = TokenHasher.NewToken();
        user.SetPendingToken(user.HasPendingInvitation ? UserTokenPurpose.Invitation : UserTokenPurpose.PasswordReset, TokenHasher.Hash(raw), now.AddHours(2), now);

        var link = user.HasPendingInvitation ? _links.Invitation(raw) : _links.PasswordReset(raw);
        var body = $"Hola {user.DisplayName},\n\nRecibimos un pedido para restablecer tu contraseña de RepairShop.\n" +
                   $"Ingresá a este link (vence en 2 horas):\n{link}\n\nSi no fuiste vos, ignorá este mensaje.";
        var item = new NotificationOutboxItem(user.ShopId, NotificationChannel.Email, user.Email, "Restablecer contraseña", body,
            OutboxStatus.Pending, $"user:{user.Id}:reset:{now:yyyyMMddHHmmss}", "user", user.Id, now);
        item.SetOrigin("auth.password.reset", null);
        await _outbox.AddAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<TokenInfoResponse> GetTokenInfoAsync(string rawToken, CancellationToken ct)
    {
        var user = await FindUserByPendingTokenAsync(rawToken, ct);
        var shop = await _shops.GetByIdAsync(user.ShopId, ct);
        return new TokenInfoResponse(user.Email, user.DisplayName, shop?.Name ?? "", user.PendingTokenPurpose?.ToString() ?? "", user.PendingTokenExpiresAtUtc ?? _clock.UtcNow);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await FindUserByPendingTokenAsync(req.Token, ct);
        if (!user.IsPendingTokenValid(UserTokenPurpose.PasswordReset, TokenHasher.Hash(req.Token), now))
            throw new DomainException("El link para restablecer la contraseña es inválido o venció.");

        PasswordPolicy.Validate(req.NewPassword);
        user.ChangePassword(_hasher.Hash(req.NewPassword), now);
        user.MarkEmailVerified(now); // the link reached the inbox
        foreach (var t in await _refreshTokens.ListActiveByUserAsync(user.Id, now, ct)) t.Revoke("password_reset", now);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<AuthResult> AcceptInvitationAsync(AcceptInvitationRequest req, string? ip, string? userAgent, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await FindUserByPendingTokenAsync(req.Token, ct);
        if (!user.IsPendingTokenValid(UserTokenPurpose.Invitation, TokenHasher.Hash(req.Token), now))
            throw new DomainException("La invitación es inválida o venció. Pedile al administrador que te envíe una nueva.");
        if (!user.IsActive) throw new DomainException("Tu usuario está desactivado.");

        PasswordPolicy.Validate(req.Password);
        if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.UpdateProfile(req.DisplayName, user.Role, now);
        user.ChangePassword(_hasher.Hash(req.Password), now);
        user.MarkEmailVerified(now); // the invitation reached the inbox
        user.RegisterLogin(now);

        var shops = await GetAccessibleShopsAsync(user, ct);
        var target = shops.FirstOrDefault(s => s.IsHome) ?? shops.FirstOrDefault()
                     ?? throw new UnauthorizedException("Tu usuario no tiene sucursales activas.");
        return await IssueAsync(user, target, shops, Guid.Empty, ip, userAgent, ct);
    }

    /// <summary>Opens a session for a user that was just created (self-service signup).</summary>
    public async Task<AuthResult> SignInAsync(AppUser user, string? ip, string? userAgent, CancellationToken ct)
    {
        var shops = await GetAccessibleShopsAsync(user, ct);
        var target = shops.FirstOrDefault(s => s.IsHome) ?? throw new UnauthorizedException("Tu usuario no tiene sucursales activas.");
        user.RegisterLogin(_clock.UtcNow);
        return await IssueAsync(user, target, shops, Guid.Empty, ip, userAgent, ct);
    }

    public async Task VerifyEmailAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) throw new DomainException("El link es inválido.");
        var user = await _users.GetByEmailVerificationTokenHashAsync(TokenHasher.Hash(rawToken), ct)
                   ?? throw new DomainException("El link es inválido o ya fue usado.");
        user.VerifyEmail(TokenHasher.Hash(rawToken), _clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task ResendEmailVerificationAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct) ?? throw new UnauthorizedException("Sesión inválida.");
        if (user.IsEmailVerified) return;
        await QueueEmailVerificationAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>Stores a fresh verification token on the user and queues the email (caller saves).</summary>
    public async Task QueueEmailVerificationAsync(AppUser user, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var raw = TokenHasher.NewToken();
        user.RequestEmailVerification(TokenHasher.Hash(raw), now);
        var body = $"Hola {user.DisplayName},\n\nConfirmá tu email para terminar de crear tu cuenta de RepairShop:\n{_links.VerifyEmail(raw)}\n\n" +
                   "Si no creaste una cuenta, ignorá este mensaje.";
        var item = new NotificationOutboxItem(user.ShopId, NotificationChannel.Email, user.Email, "Confirmá tu email", body,
            OutboxStatus.Pending, $"user:{user.Id}:verify:{now:yyyyMMddHHmmss}", "user", user.Id, now);
        item.SetOrigin("auth.email.verify", null);
        await _outbox.AddAsync(item, ct);
    }

    // ---------------------------------------------------------------------------------------------

    public async Task<List<ShopAccessInfo>> GetAccessibleShopsAsync(AppUser user, CancellationToken ct)
    {
        var accesses = await _access.ListByUserAsync(user.Id, ct);
        var shopIds = accesses.Select(a => a.ShopId).Append(user.ShopId).Distinct().ToList();
        var shops = (await _shops.GetByIdsAsync(shopIds, ct)).Where(s => s.IsActive).ToDictionary(s => s.Id);

        var result = new List<ShopAccessInfo>();
        if (shops.TryGetValue(user.ShopId, out var home)) result.Add(new ShopAccessInfo(home.Id, home.Name, home.OrganizationId, user.Role, true));
        foreach (var a in accesses.Where(a => a.ShopId != user.ShopId))
        {
            if (shops.TryGetValue(a.ShopId, out var s)) result.Add(new ShopAccessInfo(s.Id, s.Name, s.OrganizationId, a.Role, false));
        }

        return result;
    }

    private async Task<AuthResult> IssueAsync(
        AppUser user,
        ShopAccessInfo shop,
        IReadOnlyList<ShopAccessInfo> shops,
        Guid familyId,
        string? ip,
        string? userAgent,
        CancellationToken ct,
        RefreshToken? rotate = null)
    {
        var now = _clock.UtcNow;
        var access = _jwt.CreateAccessToken(user, shop.ShopId, shop.Role, shop.OrganizationId);

        var raw = TokenHasher.NewToken();
        var refreshExpires = now.Add(_jwt.RefreshTokenLifetime);
        var refresh = new RefreshToken(user.Id, shop.ShopId, TokenHasher.Hash(raw), familyId, now, refreshExpires, ip, userAgent);
        await _refreshTokens.AddAsync(refresh, ct);
        rotate?.Revoke("rotated", now, refresh.Id);

        await _uow.SaveChangesAsync(ct);

        var shopEntity = await _shops.GetByIdAsync(shop.ShopId, ct);
        var response = new LoginResponse(
            access.Token,
            ToUserResponse(user, shop, shopEntity),
            access.ExpiresAtUtc,
            shops.Select(s => s.ToResponse()).ToList(),
            Permissions.For(shop.Role));

        return new AuthResult(response, raw, refreshExpires);
    }

    private async Task<RefreshToken> FindTokenAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) throw new UnauthorizedException("No hay una sesión activa.");
        return await _refreshTokens.GetByHashAsync(TokenHasher.Hash(rawToken), ct)
               ?? throw new UnauthorizedException("La sesión expiró. Volvé a iniciar sesión.");
    }

    private async Task RevokeFamilyAsync(RefreshToken token, string reason, DateTime now, CancellationToken ct)
    {
        foreach (var t in await _refreshTokens.ListByFamilyAsync(token.UserId, token.FamilyId, ct)) t.Revoke(reason, now);
    }

    private async Task<AppUser> FindUserByPendingTokenAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) throw new DomainException("El link es inválido.");
        return await _users.GetByPendingTokenHashAsync(TokenHasher.Hash(rawToken), ct)
               ?? throw new DomainException("El link es inválido o ya fue usado.");
    }

    private static UserResponse ToUserResponse(AppUser user, ShopAccessInfo shop, Shop? shopEntity)
        => new(user.Id, shop.ShopId, user.Email, user.DisplayName, shop.Role.ToString(), shopEntity?.Name ?? shop.ShopName, shop.OrganizationId, user.IsEmailVerified);
}

public sealed record ShopAccessInfo(Guid ShopId, string ShopName, Guid OrganizationId, UserRole Role, bool IsHome)
{
    public ShopAccessResponse ToResponse() => new(ShopId, ShopName, Role.ToString(), IsHome);
}

public sealed record MeResponse(UserResponse User, IReadOnlyList<ShopAccessResponse> Shops, IReadOnlyList<string> Permissions);
