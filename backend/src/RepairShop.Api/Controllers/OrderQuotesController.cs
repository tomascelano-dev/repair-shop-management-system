using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Quotes;

namespace RepairShop.Api.Controllers;

/// <summary>
/// Itemized quotes (versions). Draft → Sent → Approved/Rejected/Expired. Approving a quote sets the
/// agreed price of the order and reserves the stock of its parts.
/// </summary>
[ApiController]
[Route("api/v1/orders/{orderId:guid}/quotes")]
[Authorize]
public sealed class OrderQuotesController : ControllerBase
{
    private readonly QuoteService _quotes;

    public OrderQuotesController(QuoteService quotes) => _quotes = quotes;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<QuoteResponse>>>> List(Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await _quotes.ListAsync(CurrentUser.GetShopId(User), orderId, ct)));

    [HttpGet("{quoteId:guid}")]
    public async Task<ActionResult<ApiResponse<QuoteResponse>>> Get(Guid orderId, Guid quoteId, CancellationToken ct)
        => Ok(Envelope.Ok(await _quotes.GetAsync(CurrentUser.GetShopId(User), orderId, quoteId, ct)));

    [HttpPost]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<QuoteResponse>>> Create(Guid orderId, [FromBody] SaveQuoteRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _quotes.CreateAsync(CurrentUser.GetShopId(User), orderId, body, CurrentUser.GetActor(User), ct)));

    [HttpPut("{quoteId:guid}")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<QuoteResponse>>> Update(Guid orderId, Guid quoteId, [FromBody] SaveQuoteRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _quotes.UpdateAsync(CurrentUser.GetShopId(User), orderId, quoteId, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Marks the quote as sent (sets its validity) and queues the message with the approval link.</summary>
    [HttpPost("{quoteId:guid}/send")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<QuoteActionResponse>>> Send(Guid orderId, Guid quoteId, [FromBody] SendQuoteRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _quotes.SendAsync(CurrentUser.GetShopId(User), orderId, quoteId, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Approval received by phone/WhatsApp/in person (the customer can also approve from the portal).</summary>
    [HttpPost("{quoteId:guid}/approve")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<QuoteActionResponse>>> Approve(Guid orderId, Guid quoteId, [FromBody] DecideQuoteRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _quotes.ApproveAsync(CurrentUser.GetShopId(User), orderId, quoteId, body, CurrentUser.GetActor(User), ct)));

    [HttpPost("{quoteId:guid}/reject")]
    [Authorize(Policy = Policies.OrdersWork)]
    public async Task<ActionResult<ApiResponse<QuoteActionResponse>>> Reject(Guid orderId, Guid quoteId, [FromBody] DecideQuoteRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _quotes.RejectAsync(CurrentUser.GetShopId(User), orderId, quoteId, body, CurrentUser.GetActor(User), ct)));
}
