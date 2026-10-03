using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Admin;
using RepairShop.Application.Contracts;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly UserAdminService _users;
    private readonly IMemoryCache _cache;

    public UsersController(UserAdminService users, IMemoryCache cache)
    {
        _users = users;
        _cache = cache;
    }

    private T Evict<T>(Guid userId, T result)
    {
        SecurityStampValidator.Evict(_cache, userId);
        return result;
    }

    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<List<UserAdminResponse>>>> List(CancellationToken ct)
        => Ok(Envelope.Ok(await _users.ListAsync(CurrentUser.GetShopId(User), ct)));

    /// <summary>Users that can be assigned as technicians (any staff member can read it).</summary>
    [HttpGet("assignable")]
    public async Task<ActionResult<ApiResponse<List<UserAdminResponse>>>> Assignable(CancellationToken ct)
        => Ok(Envelope.Ok(await _users.ListAssignableAsync(CurrentUser.GetShopId(User), ct)));

    /// <summary>Creates the user with a pending invitation and returns the link to share (also emailed when configured).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<UserLinkResponse>>> Invite([FromBody] CreateUserRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _users.InviteAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<UserAdminResponse>>> Update(Guid id, [FromBody] UpdateUserRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(Evict(id, await _users.UpdateAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct))));

    [HttpPost("{id:guid}/reset-link")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<UserLinkResponse>>> ResetLink(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _users.CreatePasswordResetLinkAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct)));

    [HttpPost("{id:guid}/shops")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<UserAdminResponse>>> GrantShop(Guid id, [FromBody] GrantShopAccessRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(Evict(id, await _users.GrantShopAccessAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct))));

    [HttpDelete("{id:guid}/shops/{shopId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<UserAdminResponse>>> RevokeShop(Guid id, Guid shopId, CancellationToken ct)
        => Ok(Envelope.Ok(Evict(id, await _users.RevokeShopAccessAsync(CurrentUser.GetShopId(User), id, shopId, CurrentUser.GetActor(User), ct))));
}
