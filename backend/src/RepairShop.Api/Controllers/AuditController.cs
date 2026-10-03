using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;

namespace RepairShop.Api.Controllers;

/// <summary>Who did what and when (orders, payments, cash, stock, users, settings...).</summary>
[ApiController]
[Route("api/v1/audit")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AuditController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditEventResponse>>>> Search(
        [FromServices] IAuditEventRepository audit,
        [FromQuery] string? entityType, [FromQuery] Guid? entityId, [FromQuery] string? action, [FromQuery] Guid? userId,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var (items, total) = await audit.SearchAsync(CurrentUser.GetShopId(User),
            new AuditSearchOptions(entityType, entityId, action, userId, dateFrom?.ToUniversalTime(), dateTo?.ToUniversalTime(), Math.Max(0, skip), Math.Clamp(take, 1, 200)), ct);

        Response.Headers[HttpExtensions.TotalCountHeader] = total.ToString();
        return Ok(Envelope.Ok<IReadOnlyList<AuditEventResponse>>(items
            .Select(e => new AuditEventResponse(e.Id, e.EntityType, e.EntityId, e.Action, e.ActorUserId, e.ActorEmail, e.DataJson, e.CreatedAtUtc))
            .ToList()));
    }
}
