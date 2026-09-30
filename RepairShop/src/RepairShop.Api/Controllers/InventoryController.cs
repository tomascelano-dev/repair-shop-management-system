using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Auditing;
using RepairShop.Domain.Inventory;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/inventory")]
[Authorize(Policy = Policies.StaffOnly)]
public sealed class InventoryController : ControllerBase
{
    private const string EntityTypeInventoryItem = "inventory_item";
    private const string EntityTypeRepairOrder = "repair_order";

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<InventoryItemResponse>>>> List(
        [FromServices] IInventoryItemRepository repo,
        [FromQuery] string? q = null,
        [FromQuery] bool includeInactive = false,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var (items, total) = await repo.SearchAsync(shopId, new InventorySearchOptions(
            Q: q,
            IncludeInactive: includeInactive,
            DateFromUtc: dateFrom?.ToUniversalTime(),
            DateToUtc: dateTo?.ToUniversalTime(),
            SortBy: sortBy,
            SortDir: sortDir,
            Skip: skip,
            Take: take
        ), ct);

        Response.Headers["X-Total-Count"] = total.ToString();

        var res = items.Select(ToResponse).ToList();
        return Ok(new ApiResponse<List<InventoryItemResponse>>(res));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> GetById(
        [FromServices] IInventoryItemRepository repo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var item = await repo.GetByIdAsync(shopId, id, ct);
        if (item is null) return NotFound();
        return Ok(new ApiResponse<InventoryItemResponse>(ToResponse(item)));
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> Create(
        [FromServices] IInventoryItemRepository repo,
        [FromServices] IInventoryAdjustmentRepository adjustments,
        [FromServices] IAuditEventRepository audit,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        [FromBody] CreateInventoryItemRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        var userId = CurrentUser.GetUserId(User);
        var email = CurrentUser.GetEmail(User);
        if (shopId == Guid.Empty || userId == Guid.Empty) return Unauthorized();

        var item = new InventoryItem(shopId, body.Sku, body.Name, body.InitialQuantity, body.UnitCost, body.UnitCostCurrency, body.IsActive, clock.UtcNow);
        await repo.AddAsync(item, ct);

        if (body.InitialQuantity != 0)
        {
            var adj = new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.Correction, body.InitialQuantity, "initial_quantity", null, userId, clock.UtcNow);
            await adjustments.AddAsync(adj, ct);
        }

        await audit.AddAsync(new AuditEvent(
            shopId: shopId,
            entityType: EntityTypeInventoryItem,
            entityId: item.Id,
            action: "inventory_item_created",
            actorUserId: userId,
            actorEmail: email,
            dataJson: JsonSerializer.Serialize(new { itemId = item.Id, sku = item.Sku, name = item.Name }),
            nowUtc: clock.UtcNow
        ), ct);

        await uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, new ApiResponse<InventoryItemResponse>(ToResponse(item)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> Update(
        [FromServices] IInventoryItemRepository repo,
        [FromServices] IAuditEventRepository audit,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] UpdateInventoryItemRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        var userId = CurrentUser.GetUserId(User);
        var email = CurrentUser.GetEmail(User);
        if (shopId == Guid.Empty || userId == Guid.Empty) return Unauthorized();

        var item = await repo.GetByIdAsync(shopId, id, ct);
        if (item is null) return NotFound();

        item.Update(body.Name, body.IsActive, clock.UtcNow);

        await audit.AddAsync(new AuditEvent(
            shopId: shopId,
            entityType: EntityTypeInventoryItem,
            entityId: item.Id,
            action: "inventory_item_updated",
            actorUserId: userId,
            actorEmail: email,
            dataJson: JsonSerializer.Serialize(new { itemId = item.Id }),
            nowUtc: clock.UtcNow
        ), ct);

        await uow.SaveChangesAsync(ct);
        return Ok(new ApiResponse<InventoryItemResponse>(ToResponse(item)));
    }

    [HttpGet("{id:guid}/adjustments")]
    public async Task<ActionResult<ApiResponse<List<InventoryAdjustmentResponse>>>> ListAdjustments(
        [FromServices] IInventoryAdjustmentRepository repo,
        Guid id,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var list = await repo.ListByItemAsync(shopId, id, skip, take, ct);
        var res = list.Select(a => new InventoryAdjustmentResponse(a.Id, a.InventoryItemId, a.Type, a.DeltaQuantity, a.Reason, a.RepairOrderId, a.CreatedByUserId, a.CreatedAtUtc)).ToList();
        return Ok(new ApiResponse<List<InventoryAdjustmentResponse>>(res));
    }

    [HttpPost("{id:guid}/adjustments")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> AddAdjustment(
        [FromServices] IInventoryItemRepository items,
        [FromServices] IInventoryAdjustmentRepository adjustments,
        [FromServices] IAuditEventRepository audit,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] CreateInventoryAdjustmentRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        var userId = CurrentUser.GetUserId(User);
        var email = CurrentUser.GetEmail(User);
        if (shopId == Guid.Empty || userId == Guid.Empty) return Unauthorized();

        var item = await items.GetByIdAsync(shopId, id, ct);
        if (item is null) return NotFound();

        // Apply inventory delta first (validates cannot go below 0)
        item.ApplyDelta(body.DeltaQuantity, clock.UtcNow);

        var adj = new InventoryAdjustment(shopId, item.Id, body.Type, body.DeltaQuantity, body.Reason, null, userId, clock.UtcNow);
        await adjustments.AddAsync(adj, ct);

        await audit.AddAsync(new AuditEvent(
            shopId: shopId,
            entityType: EntityTypeInventoryItem,
            entityId: item.Id,
            action: "inventory_adjusted",
            actorUserId: userId,
            actorEmail: email,
            dataJson: JsonSerializer.Serialize(new { itemId = item.Id, delta = body.DeltaQuantity, type = body.Type.ToString(), reason = body.Reason }),
            nowUtc: clock.UtcNow
        ), ct);

        await uow.SaveChangesAsync(ct);
        return Ok(new ApiResponse<InventoryItemResponse>(ToResponse(item)));
    }

    // PRO: consume parts on an order (creates part usage + inventory adjustment)
    [HttpPost("use-on-order/{orderId:guid}")]
    public async Task<ActionResult<ApiResponse<RepairOrderPartUsageResponse>>> UseOnOrder(
        [FromServices] IRepairOrderRepository orders,
        [FromServices] IInventoryItemRepository items,
        [FromServices] IInventoryAdjustmentRepository adjustments,
        [FromServices] IRepairOrderPartUsageRepository usages,
        [FromServices] IAuditEventRepository audit,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid orderId,
        [FromBody] UsePartOnOrderRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        var userId = CurrentUser.GetUserId(User);
        var email = CurrentUser.GetEmail(User);
        if (shopId == Guid.Empty || userId == Guid.Empty) return Unauthorized();

        var order = await orders.GetByIdAsync(shopId, orderId, ct);
        if (order is null) return NotFound("Order not found.");

        var item = await items.GetByIdAsync(shopId, body.InventoryItemId, ct);
        if (item is null) return NotFound("Inventory item not found.");

        var qty = body.QuantityUsed;
        if (qty <= 0) return BadRequest("QuantityUsed must be > 0.");

        // Decrease inventory
        item.ApplyDelta(-qty, clock.UtcNow);

        // Track usage
        var usage = new RepairOrderPartUsage(shopId, orderId, item.Id, qty, body.UnitPrice, body.UnitPriceCurrency, userId, clock.UtcNow);
        await usages.AddAsync(usage, ct);

        // Adjustment record
        var adj = new InventoryAdjustment(shopId, item.Id, InventoryAdjustmentType.Consumption, -qty, "used_on_order", orderId, userId, clock.UtcNow);
        await adjustments.AddAsync(adj, ct);

        // Audit both entities (order-level)
        await audit.AddAsync(new AuditEvent(
            shopId: shopId,
            entityType: EntityTypeRepairOrder,
            entityId: orderId,
            action: "part_used",
            actorUserId: userId,
            actorEmail: email,
            dataJson: JsonSerializer.Serialize(new { orderId, inventoryItemId = item.Id, sku = item.Sku, quantityUsed = qty, unitPrice = usage.UnitPrice, currency = usage.UnitPriceCurrency }),
            nowUtc: clock.UtcNow
        ), ct);

        await uow.SaveChangesAsync(ct);

        var res = new RepairOrderPartUsageResponse(usage.Id, usage.RepairOrderId, usage.InventoryItemId, usage.QuantityUsed, usage.UnitPrice, usage.UnitPriceCurrency, usage.CreatedByUserId, usage.CreatedAtUtc);
        return Ok(new ApiResponse<RepairOrderPartUsageResponse>(res));
    }

    [HttpGet("parts/by-order/{orderId:guid}")]
    public async Task<ActionResult<ApiResponse<List<RepairOrderPartUsageResponse>>>> ListOrderParts(
        [FromServices] IRepairOrderPartUsageRepository repo,
        Guid orderId,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var list = await repo.ListByOrderAsync(shopId, orderId, ct);
        var res = list.Select(u => new RepairOrderPartUsageResponse(u.Id, u.RepairOrderId, u.InventoryItemId, u.QuantityUsed, u.UnitPrice, u.UnitPriceCurrency, u.CreatedByUserId, u.CreatedAtUtc)).ToList();
        return Ok(new ApiResponse<List<RepairOrderPartUsageResponse>>(res));
    }

    private static InventoryItemResponse ToResponse(InventoryItem i)
        => new(i.Id, i.ShopId, i.Sku, i.Name, i.QuantityOnHand, i.UnitCost, i.UnitCostCurrency, i.IsActive, i.CreatedAtUtc, i.UpdatedAtUtc);
}
