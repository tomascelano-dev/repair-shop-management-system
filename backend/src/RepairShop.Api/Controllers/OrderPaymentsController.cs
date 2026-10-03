using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Fiscal;
using RepairShop.Application.Payments;

namespace RepairShop.Api.Controllers;

/// <summary>Deposits, payments, refunds, online payment links and invoices of an order.</summary>
[ApiController]
[Route("api/v1/orders/{orderId:guid}")]
[Authorize]
public sealed class OrderPaymentsController : ControllerBase
{
    private readonly OrderPaymentService _payments;

    public OrderPaymentsController(OrderPaymentService payments) => _payments = payments;

    [HttpGet("financials")]
    public async Task<ActionResult<ApiResponse<OrderFinancialsResponse>>> Financials(Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await _payments.GetFinancialsAsync(CurrentUser.GetShopId(User), orderId, ct)));

    [HttpGet("payments")]
    public async Task<ActionResult<ApiResponse<List<RepairOrderPaymentResponse>>>> List(Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await _payments.ListAsync(CurrentUser.GetShopId(User), orderId, ct)));

    /// <summary>Registers a deposit or payment (validated against the balance; cash goes to the open register).</summary>
    [HttpPost("payments")]
    [Authorize(Policy = Policies.Sales)]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<RepairOrderPaymentResponse>>> Add(Guid orderId, [FromBody] CreateRepairOrderPaymentRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _payments.AddAsync(CurrentUser.GetShopId(User), orderId, body, CurrentUser.GetActor(User), ct)));

    [HttpPost("payments/{paymentId:guid}/refund")]
    [Authorize(Policy = Policies.Sales)]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<RepairOrderPaymentResponse>>> Refund(Guid orderId, Guid paymentId, [FromBody] RefundOrderPaymentRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _payments.RefundAsync(CurrentUser.GetShopId(User), orderId, paymentId, body, CurrentUser.GetActor(User), ct)));

    [HttpGet("payment-links")]
    public async Task<ActionResult<ApiResponse<List<PaymentLinkResponse>>>> Links([FromServices] PaymentLinkService links, Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await links.ListForOrderAsync(CurrentUser.GetShopId(User), orderId, ct)));

    /// <summary>Creates (or reuses) a Mercado Pago link for the current balance.</summary>
    [HttpPost("payment-links")]
    [Authorize(Policy = Policies.Sales)]
    public async Task<ActionResult<ApiResponse<PaymentLinkResponse>>> CreateLink([FromServices] PaymentLinkService links, Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await links.CreateForOrderAsync(CurrentUser.GetShopId(User), orderId, CurrentUser.GetActor(User), ct)));

    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<List<FiscalInvoiceResponse>>>> Invoices([FromServices] FiscalService fiscal, Guid orderId, CancellationToken ct)
        => Ok(Envelope.Ok(await fiscal.ListBySourceAsync(CurrentUser.GetShopId(User), FiscalService.SourceOrder, orderId, ct)));

    /// <summary>Issues an electronic invoice (ARCA) for the amount paid on the order.</summary>
    [HttpPost("invoices")]
    [Authorize(Policy = Policies.Sales)]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<FiscalInvoiceResponse>>> Invoice([FromServices] FiscalService fiscal, Guid orderId, [FromBody] IssueInvoiceRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await fiscal.IssueForOrderAsync(CurrentUser.GetShopId(User), orderId, body, CurrentUser.GetActor(User), ct)));
}
