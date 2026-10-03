using System.Text.Json;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Security;
using RepairShop.Domain.Auditing;
using RepairShop.Domain.Common;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.Users;

namespace RepairShop.Application.Admin;

public sealed class UserAdminService
{
    private const string EntityType = "user";

    private readonly IUserRepository _users;
    private readonly IUserShopAccessRepository _access;
    private readonly IShopRepository _shops;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly INotificationOutboxRepository _outbox;
    private readonly IAuditEventRepository _audit;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly IAppLinks _links;

    public UserAdminService(
        IUserRepository users,
        IUserShopAccessRepository access,
        IShopRepository shops,
        IRefreshTokenRepository refreshTokens,
        INotificationOutboxRepository outbox,
        IAuditEventRepository audit,
        IPasswordHasher hasher,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IAppLinks links)
    {
        _users = users;
        _access = access;
        _shops = shops;
        _refreshTokens = refreshTokens;
        _outbox = outbox;
        _audit = audit;
        _hasher = hasher;
        _uow = uow;
        _clock = clock;
        _links = links;
    }

    public async Task<List<UserAdminResponse>> ListAsync(Guid shopId, CancellationToken ct)
    {
        var users = await _users.ListForShopAsync(shopId, ct);
        var result = new List<UserAdminResponse>();
        foreach (var u in users) result.Add(await ToResponseAsync(u, shopId, ct));
        return result;
    }

    /// <summary>Technicians (and admins) that can be assigned to orders in this shop.</summary>
    public async Task<List<UserAdminResponse>> ListAssignableAsync(Guid shopId, CancellationToken ct)
        => (await ListAsync(shopId, ct)).Where(u => u.IsActive && u.Role is nameof(UserRole.Tech) or nameof(UserRole.Admin)).ToList();

    public async Task<UserLinkResponse> InviteAsync(Guid shopId, CreateUserRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var email = AppUser.NormalizeEmail(req.Email);
        if (await _users.GetByEmailAsync(email, ct) is not null)
            throw new ConflictException("Ya existe un usuario con ese email.");

        // Random unusable password until the invitation is accepted.
        var user = new AppUser(shopId, email, req.DisplayName, req.Role, _hasher.Hash(TokenHasher.NewToken()), now);
        var raw = TokenHasher.NewToken();
        var expires = now.AddDays(7);
        user.SetPendingToken(UserTokenPurpose.Invitation, TokenHasher.Hash(raw), expires, now);
        await _users.AddAsync(user, ct);

        var shop = await _shops.GetByIdAsync(shopId, ct);
        var url = _links.Invitation(raw);
        await EnqueueEmailAsync(shopId, user, "Invitación a RepairShop",
            $"Hola {user.DisplayName},\n\n{actor.Email ?? "El administrador"} te invitó a usar RepairShop en {shop?.Name}.\n" +
            $"Creá tu contraseña desde este link (vence en 7 días):\n{url}", "invite", now, ct);

        await AuditAsync(shopId, user.Id, "user_invited", actor, new { email, role = req.Role.ToString() }, ct);
        await _uow.SaveChangesAsync(ct);
        return new UserLinkResponse(await ToResponseAsync(user, shopId, ct), url, expires);
    }

    public async Task<UserAdminResponse> UpdateAsync(Guid shopId, Guid userId, UpdateUserRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await GetManagedUserAsync(shopId, userId, ct);

        if (user.Id == actor.UserId && !req.IsActive) throw new DomainException("No podés desactivar tu propio usuario.");

        var currentRole = await GetRoleInShopAsync(user, shopId, ct);
        var losesAdmin = currentRole == UserRole.Admin && (req.Role != UserRole.Admin || !req.IsActive);
        if (losesAdmin && await _users.CountActiveAdminsAsync(shopId, ct) <= 1)
            throw new DomainException("Tiene que quedar al menos un administrador activo en la sucursal.");

        if (user.ShopId == shopId)
        {
            user.UpdateProfile(req.DisplayName, req.Role, now);
        }
        else
        {
            var access = await _access.GetAsync(user.Id, shopId, ct) ?? throw new NotFoundException("Usuario no encontrado.");
            access.ChangeRole(req.Role);
            user.UpdateProfile(req.DisplayName, user.Role, now);
            user.RotateSecurityStamp(now);
        }

        if (user.IsActive != req.IsActive)
        {
            user.SetActive(req.IsActive, now);
            if (!req.IsActive)
                foreach (var t in await _refreshTokens.ListActiveByUserAsync(user.Id, now, ct)) t.Revoke("user_deactivated", now);
        }

        await AuditAsync(shopId, user.Id, "user_updated", actor, new { role = req.Role.ToString(), req.IsActive }, ct);
        await _uow.SaveChangesAsync(ct);
        return await ToResponseAsync(user, shopId, ct);
    }

    public async Task<UserLinkResponse> CreatePasswordResetLinkAsync(Guid shopId, Guid userId, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await GetManagedUserAsync(shopId, userId, ct);
        if (!user.IsActive) throw new DomainException("El usuario está desactivado.");

        var raw = TokenHasher.NewToken();
        var invitation = user.HasPendingInvitation;
        var expires = now.AddDays(invitation ? 7 : 1);
        user.SetPendingToken(invitation ? UserTokenPurpose.Invitation : UserTokenPurpose.PasswordReset, TokenHasher.Hash(raw), expires, now);
        var url = invitation ? _links.Invitation(raw) : _links.PasswordReset(raw);

        await EnqueueEmailAsync(shopId, user, invitation ? "Invitación a RepairShop" : "Restablecer contraseña",
            $"Hola {user.DisplayName},\n\nUsá este link para {(invitation ? "crear" : "restablecer")} tu contraseña:\n{url}", invitation ? "invite" : "reset", now, ct);

        await AuditAsync(shopId, user.Id, invitation ? "user_invitation_resent" : "user_password_reset_link", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return new UserLinkResponse(await ToResponseAsync(user, shopId, ct), url, expires);
    }

    public async Task<UserAdminResponse> GrantShopAccessAsync(Guid currentShopId, Guid userId, GrantShopAccessRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await GetManagedUserAsync(currentShopId, userId, ct);
        var current = await _shops.GetByIdAsync(currentShopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var target = await _shops.GetByIdAsync(req.ShopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        if (target.OrganizationId != current.OrganizationId) throw new ForbiddenException("La sucursal pertenece a otra organización.");
        if (target.Id == user.ShopId) throw new DomainException("Esa es la sucursal principal del usuario.");

        var existing = await _access.GetAsync(user.Id, target.Id, ct);
        if (existing is null) await _access.AddAsync(new UserShopAccess(user.Id, target.Id, req.Role, now), ct);
        else existing.ChangeRole(req.Role);

        user.RotateSecurityStamp(now);
        await AuditAsync(currentShopId, user.Id, "user_shop_access_granted", actor, new { shopId = target.Id, role = req.Role.ToString() }, ct);
        await _uow.SaveChangesAsync(ct);
        return await ToResponseAsync(user, currentShopId, ct);
    }

    public async Task<UserAdminResponse> RevokeShopAccessAsync(Guid currentShopId, Guid userId, Guid shopId, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var user = await GetManagedUserAsync(currentShopId, userId, ct);
        var access = await _access.GetAsync(user.Id, shopId, ct) ?? throw new NotFoundException("El usuario no tiene acceso a esa sucursal.");
        _access.Remove(access);
        user.RotateSecurityStamp(now);
        await AuditAsync(currentShopId, user.Id, "user_shop_access_revoked", actor, new { shopId }, ct);
        await _uow.SaveChangesAsync(ct);
        return await ToResponseAsync(user, currentShopId, ct);
    }

    private async Task<AppUser> GetManagedUserAsync(Guid shopId, Guid userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Usuario no encontrado.");
        if (user.ShopId != shopId && await _access.GetAsync(user.Id, shopId, ct) is null)
            throw new NotFoundException("Usuario no encontrado.");
        return user;
    }

    private async Task<UserRole> GetRoleInShopAsync(AppUser user, Guid shopId, CancellationToken ct)
        => user.ShopId == shopId ? user.Role : (await _access.GetAsync(user.Id, shopId, ct))?.Role ?? user.Role;

    private async Task<UserAdminResponse> ToResponseAsync(AppUser user, Guid shopId, CancellationToken ct)
    {
        var accesses = await _access.ListByUserAsync(user.Id, ct);
        var shopIds = accesses.Select(a => a.ShopId).Append(user.ShopId).Distinct().ToList();
        var shops = (await _shops.GetByIdsAsync(shopIds, ct)).ToDictionary(s => s.Id);

        var list = new List<ShopAccessResponse>();
        if (shops.TryGetValue(user.ShopId, out var home)) list.Add(new ShopAccessResponse(home.Id, home.Name, user.Role.ToString(), true));
        list.AddRange(accesses.Where(a => a.ShopId != user.ShopId && shops.ContainsKey(a.ShopId))
            .Select(a => new ShopAccessResponse(a.ShopId, shops[a.ShopId].Name, a.Role.ToString(), false)));

        var role = user.ShopId == shopId ? user.Role : accesses.FirstOrDefault(a => a.ShopId == shopId)?.Role ?? user.Role;
        return new UserAdminResponse(user.Id, user.Email, user.DisplayName, role.ToString(), user.IsActive, user.ShopId == shopId,
            user.HasPendingInvitation, user.LastLoginAtUtc, user.CreatedAtUtc, list);
    }

    private async Task EnqueueEmailAsync(Guid shopId, AppUser user, string title, string body, string kind, DateTime now, CancellationToken ct)
    {
        var item = new NotificationOutboxItem(shopId, NotificationChannel.Email, user.Email, title, body, OutboxStatus.Pending,
            $"user:{user.Id}:{kind}:{now:yyyyMMddHHmmssfff}", EntityType, user.Id, now);
        item.SetOrigin($"auth.{kind}", null);
        await _outbox.AddAsync(item, ct);
    }

    private Task AuditAsync(Guid shopId, Guid userId, string action, Actor actor, object? data, CancellationToken ct)
        => _audit.AddAsync(new AuditEvent(shopId, EntityType, userId, action, actor.UserId, actor.Email,
            data is null ? null : JsonSerializer.Serialize(data), _clock.UtcNow), ct);
}
