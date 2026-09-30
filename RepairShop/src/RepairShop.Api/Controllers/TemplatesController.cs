using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Messaging;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Authorize(Policy = Policies.StaffOnly)]
public sealed class TemplatesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<MessageTemplateResponse>>>> List(
        [FromServices] IMessageTemplateRepository repo,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var list = await repo.ListAsync(shopId, includeInactive, ct);
        var res = list.Select(ToResponse).ToList();
        return Ok(new ApiResponse<List<MessageTemplateResponse>>(res));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MessageTemplateResponse>>> GetById(
        [FromServices] IMessageTemplateRepository repo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var t = await repo.GetByIdAsync(shopId, id, ct);
        if (t is null) return NotFound();

        return Ok(new ApiResponse<MessageTemplateResponse>(ToResponse(t)));
    }

    [HttpGet("by-key/{key}")]
    public async Task<ActionResult<ApiResponse<MessageTemplateResponse>>> GetByKey(
        [FromServices] IMessageTemplateRepository repo,
        string key,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var t = await repo.GetByKeyAsync(shopId, key, ct);
        if (t is null) return NotFound();
        return Ok(new ApiResponse<MessageTemplateResponse>(ToResponse(t)));
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<MessageTemplateResponse>>> Create(
        [FromServices] IMessageTemplateRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        [FromBody] CreateMessageTemplateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var template = new MessageTemplate(shopId, body.Key, body.Title, body.Body, body.IsActive, clock.UtcNow);
        await repo.AddAsync(template, ct);
        await uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = template.Id }, new ApiResponse<MessageTemplateResponse>(ToResponse(template)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<MessageTemplateResponse>>> Update(
        [FromServices] IMessageTemplateRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] UpdateMessageTemplateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var t = await repo.GetByIdAsync(shopId, id, ct);
        if (t is null) return NotFound();

        t.Update(body.Title, body.Body, body.IsActive, clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        return Ok(new ApiResponse<MessageTemplateResponse>(ToResponse(t)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(
        [FromServices] IMessageTemplateRepository repo,
        [FromServices] IUnitOfWork uow,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var t = await repo.GetByIdAsync(shopId, id, ct);
        if (t is null) return NotFound();

        await repo.RemoveAsync(t, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    private static MessageTemplateResponse ToResponse(MessageTemplate t)
        => new(t.Id, t.ShopId, t.Key, t.Title, t.Body, t.IsActive, t.CreatedAtUtc, t.UpdatedAtUtc);
}
