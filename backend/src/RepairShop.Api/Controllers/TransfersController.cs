using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Inventory;
using RepairShop.Domain.Inventory;

namespace RepairShop.Api.Controllers;

/// <summary>Stock transfers between branches of the same organization (send → receive).</summary>
[ApiController]
[Route("api/v1/transfers")]
[Authorize(Policy = Policies.InventoryManage)]
public sealed class TransfersController : ControllerBase
{
    private readonly StockTransferService _transfers;

    public TransfersController(StockTransferService transfers) => _transfers = transfers;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransferResponse>>>> List([FromQuery] StockTransferStatus? status, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(Envelope.Ok(Response.WithTotal(await _transfers.ListAsync(CurrentUser.GetShopId(User), status, skip, take, ct))));

    [HttpPost]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<TransferResponse>>> Create([FromBody] CreateTransferRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _transfers.CreateAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpPost("{id:guid}/receive")]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<TransferResponse>>> Receive(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _transfers.ReceiveAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct)));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<TransferResponse>>> Cancel(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _transfers.CancelAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct)));
}
