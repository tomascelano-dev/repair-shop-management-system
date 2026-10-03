using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Billing;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Inventory;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Inventory;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1")]
[RequiresModule(PlanModules.Purchasing)]
[Authorize(Policy = Policies.InventoryManage)]
public sealed class PurchasingController : ControllerBase
{
    private readonly PurchasingService _purchasing;

    public PurchasingController(PurchasingService purchasing) => _purchasing = purchasing;

    // ===== Suppliers =====

    [HttpGet("suppliers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SupplierResponse>>>> Suppliers(
        [FromQuery] string? q, [FromQuery] bool includeInactive = false, [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(Envelope.Ok(Response.WithTotal(await _purchasing.SearchSuppliersAsync(CurrentUser.GetShopId(User), q, includeInactive, skip, take, ct))));

    [HttpPost("suppliers")]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> CreateSupplier([FromBody] SupplierRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _purchasing.CreateSupplierAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpPut("suppliers/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> UpdateSupplier(Guid id, [FromBody] SupplierRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _purchasing.UpdateSupplierAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    // ===== Purchase orders: Draft → Ordered → (Partially)Received / Cancelled =====

    [HttpGet("purchase-orders")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PurchaseOrderResponse>>>> List(
        [FromQuery] PurchaseOrderStatus? status, [FromQuery] Guid? supplierId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(Envelope.Ok(Response.WithTotal(await _purchasing.SearchAsync(CurrentUser.GetShopId(User), status, supplierId, skip, take, ct))));

    [HttpGet("purchase-orders/{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> Get(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _purchasing.GetAsync(CurrentUser.GetShopId(User), id, ct)));

    [HttpPost("purchase-orders")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> Create([FromBody] SavePurchaseOrderRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _purchasing.CreateAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpPut("purchase-orders/{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> Update(Guid id, [FromBody] SavePurchaseOrderRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _purchasing.UpdateAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    [HttpPost("purchase-orders/{id:guid}/ordered")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> MarkOrdered(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _purchasing.MarkOrderedAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct)));

    [HttpPost("purchase-orders/{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> Cancel(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _purchasing.CancelAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct)));

    /// <summary>Receives goods (partial deliveries allowed): adds stock and updates the average cost.</summary>
    [HttpPost("purchase-orders/{id:guid}/receive")]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> Receive(Guid id, [FromBody] ReceivePurchaseOrderRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _purchasing.ReceiveAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));
}
