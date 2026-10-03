using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RepairShop.Api.Billing;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Billing;
using RepairShop.Application.Contracts;

namespace RepairShop.Api.Controllers;

/// <summary>Plans, the organization's subscription and the payment providers' webhooks.</summary>
[ApiController]
[Route("api/v1/billing")]
[SkipSubscriptionGate]
public sealed class BillingController : ControllerBase
{
    private readonly SubscriptionService _subscriptions;
    private readonly IMemoryCache _cache;

    public BillingController(SubscriptionService subscriptions, IMemoryCache cache)
    {
        _subscriptions = subscriptions;
        _cache = cache;
    }

    /// <summary>Public price list (pricing page). Argentina sees pesos; every other country, dollars.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Public)]
    public ActionResult<ApiResponse<PlansResponse>> PlansList([FromQuery] string? country)
        => Ok(Envelope.Ok(_subscriptions.ListPlans(country)));

    /// <summary>Public settings the web app needs before signing in (signup switch, Paddle.js token).</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Public)]
    public ActionResult<ApiResponse<BillingConfigResponse>> Config([FromServices] IOptions<BillingOptions> options)
    {
        var o = options.Value;
        return Ok(Envelope.Ok(new BillingConfigResponse(
            o.SignupEnabled,
            o.TrialDays,
            o.Paddle.IsConfigured ? o.Paddle.ClientToken : null,
            o.Paddle.IsProduction ? "production" : "sandbox")));
    }

    [HttpGet("subscription")]
    public async Task<ActionResult<ApiResponse<SubscriptionResponse>>> Get(CancellationToken ct)
        => Ok(Envelope.Ok(await _subscriptions.GetAsync(CurrentUser.GetOrganizationId(User), CurrentUser.GetUserId(User), ct)));

    [HttpPost("checkout")]
    [Authorize(Policy = Policies.AdminOnly)]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<ApiResponse<CheckoutResponse>>> Checkout([FromBody] CheckoutRequest body, CancellationToken ct)
    {
        var org = CurrentUser.GetOrganizationId(User);
        var result = await _subscriptions.CheckoutAsync(org, CurrentUser.GetUserId(User), body, CurrentUser.GetActor(User), ct);
        SubscriptionGateFilter.Evict(_cache, org);
        return Ok(Envelope.Ok(result));
    }

    [HttpPost("cancel")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<SubscriptionResponse>>> Cancel(CancellationToken ct)
    {
        var org = CurrentUser.GetOrganizationId(User);
        var result = await _subscriptions.CancelAsync(org, CurrentUser.GetUserId(User), CurrentUser.GetActor(User), ct);
        SubscriptionGateFilter.Evict(_cache, org);
        return Ok(Envelope.Ok(result));
    }

    /// <summary>Mercado Pago subscription notifications (signed with x-signature; the state is fetched from Mercado Pago).</summary>
    [HttpPost("webhooks/mercadopago")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Webhooks)]
    public async Task<IActionResult> MercadoPago(CancellationToken ct)
    {
        var (type, dataId) = await WebhookPayload.ReadMercadoPagoAsync(Request, ct);
        var notification = new MercadoPagoNotification(type, dataId, Request.Headers["x-request-id"].FirstOrDefault(), Request.Headers["x-signature"].FirstOrDefault());
        return Done(await _subscriptions.HandleMercadoPagoWebhookAsync(notification, ct));
    }

    /// <summary>Paddle Billing notifications (signed with Paddle-Signature over the raw body).</summary>
    [HttpPost("webhooks/paddle")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Webhooks)]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> Paddle(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        return Done(await _subscriptions.HandlePaddleWebhookAsync(body, Request.Headers["Paddle-Signature"].FirstOrDefault(), ct));
    }

    private IActionResult Done(WebhookOutcome outcome)
    {
        if (outcome.OrganizationId is { } org) SubscriptionGateFilter.Evict(_cache, org);
        return outcome.Accepted ? Ok() : Unauthorized();
    }
}

public sealed record BillingConfigResponse(bool SignupEnabled, int TrialDays, string? PaddleClientToken, string PaddleEnvironment);
