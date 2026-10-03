using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Documents;
using RepairShop.Application.Portal;

namespace RepairShop.Api.Controllers;

/// <summary>
/// Customer portal (no login): the unguessable order token from the tracking link/QR gives access to the
/// status, timeline, public notes, quote approval, online payment, warranty and satisfaction survey.
/// </summary>
[ApiController]
[Route("api/v1/public/orders/{token}")]
[AllowAnonymous]
[EnableRateLimiting(RateLimits.Public)]
public sealed class PublicController : ControllerBase
{
    private readonly PublicPortalService _portal;

    public PublicController(PublicPortalService portal) => _portal = portal;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PublicTrackingResponse>>> Get(string token, CancellationToken ct)
    {
        NoIndex();
        return Ok(Envelope.Ok(await _portal.GetAsync(token, ct)));
    }

    [HttpPost("quotes/{quoteId:guid}/approve")]
    public async Task<ActionResult<ApiResponse<PublicTrackingResponse>>> Approve(string token, Guid quoteId, [FromBody] PublicDecisionRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _portal.DecideQuoteAsync(token, quoteId, true, body.Note, HttpContext.ClientIp(), ct)));

    [HttpPost("quotes/{quoteId:guid}/reject")]
    public async Task<ActionResult<ApiResponse<PublicTrackingResponse>>> Reject(string token, Guid quoteId, [FromBody] PublicDecisionRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _portal.DecideQuoteAsync(token, quoteId, false, body.Note, HttpContext.ClientIp(), ct)));

    /// <summary>Mercado Pago checkout link for the current balance.</summary>
    [HttpPost("payment-link")]
    public async Task<ActionResult<ApiResponse<PublicPaymentLinkResponse>>> PaymentLink(string token, CancellationToken ct)
        => Ok(Envelope.Ok(await _portal.CreatePaymentLinkAsync(token, ct)));

    [HttpPost("feedback")]
    public async Task<ActionResult<ApiResponse<PublicFeedbackResponse>>> Feedback(string token, [FromBody] PublicFeedbackRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _portal.SubmitFeedbackAsync(token, body, ct)));

    [HttpGet("quote.pdf")]
    public async Task<IActionResult> QuotePdf([FromServices] DocumentService docs, string token, CancellationToken ct)
        => Pdf(await docs.PublicQuoteAsync(await _portal.RequireAsync(token, ct), ct));

    [HttpGet("warranty.pdf")]
    public async Task<IActionResult> WarrantyPdf([FromServices] DocumentService docs, string token, CancellationToken ct)
        => Pdf(await docs.PublicWarrantyAsync(await _portal.RequireAsync(token, ct), ct));

    private FileContentResult Pdf(GeneratedDocument doc)
    {
        NoIndex();
        Response.Headers.ContentDisposition = $"inline; filename=\"{doc.FileName}\"";
        return File(doc.Content, doc.ContentType);
    }

    private void NoIndex()
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
