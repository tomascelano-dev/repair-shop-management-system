using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RepairShop.Api.Billing;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Billing;
using RepairShop.Application.Abstractions;
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

    /// <summary>Public price list (pricing page) in the country's currency; countries not offered get Argentina's.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Public)]
    public ActionResult<ApiResponse<PlansResponse>> PlansList([FromQuery] string? country)
        => Ok(Envelope.Ok(_subscriptions.ListPlans(country)));

    /// <summary>Public settings the web app needs before signing in (signup switch, countries sold, legal contact).</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Public)]
    public ActionResult<ApiResponse<BillingConfigResponse>> Config([FromServices] IOptions<BillingOptions> options, [FromServices] IOptions<TrackingOptions> tracking)
    {
        var o = options.Value;
        var t = tracking.Value;
        return Ok(Envelope.Ok(new BillingConfigResponse(
            o.SignupEnabled,
            o.TrialDays,
            o.OfferedCountries().Select(c => new BillingCountryResponse(c, SubscriptionService.CurrencyFor(c))).ToList(),
            string.IsNullOrWhiteSpace(o.ContactEmail) ? null : o.ContactEmail.Trim(),
            string.IsNullOrWhiteSpace(o.LegalName) ? null : o.LegalName.Trim(),
            new TrackingConfigResponse(
                TrackingOptions.PublicId(t.Ga4Id),
                TrackingOptions.PublicId(t.GoogleAdsId),
                TrackingOptions.PublicId(t.GoogleAdsSignupLabel),
                TrackingOptions.PublicId(t.GoogleAdsPurchaseLabel),
                TrackingOptions.PublicId(t.MetaPixelId)),
            VisitorCountry())));
    }

    /// <summary>
    /// "Botón de arrepentimiento" (Res. 424/2020): anyone can withdraw from the contract without signing in and
    /// gets an identification code right away.
    /// </summary>
    [HttpPost("withdrawal")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<ApiResponse<WithdrawalResponse>>> Withdrawal([FromBody] WithdrawalRequestBody body, [FromServices] WithdrawalService withdrawals, CancellationToken ct)
        => Ok(Envelope.Ok(await withdrawals.RequestAsync(body, HttpContext.Connection.RemoteIpAddress?.ToString(), ct)));

    [HttpGet("subscription")]
    public async Task<ActionResult<ApiResponse<SubscriptionResponse>>> Get(CancellationToken ct)
        => Ok(Envelope.Ok(await _subscriptions.GetAsync(CurrentUser.GetOrganizationId(User), CurrentUser.GetUserId(User), ct)));

    [HttpPost("ad-consent/revoke")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> RevokeAdConsent([FromServices] IAdTrackingRepository tracking, [FromServices] IUnitOfWork uow, CancellationToken ct)
    {
        var attribution = await tracking.GetAttributionAsync(CurrentUser.GetOrganizationId(User), ct);
        if (attribution is not null)
        {
            attribution.RevokeAdConsent();
            await uow.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    [HttpPost("checkout")]
    [Authorize(Policy = Policies.AdminOnly)]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<ApiResponse<CheckoutResponse>>> Checkout([FromBody] CheckoutRequest body,
        [FromServices] IUserRepository users, [FromServices] IUserShopAccessRepository access, CancellationToken ct)
    {
        var org = CurrentUser.GetOrganizationId(User);
        await Application.Admin.BranchAuthority.RequireOrganizationAdminAsync(users, access, CurrentUser.GetUserId(User), org, ct);
        var result = await _subscriptions.CheckoutAsync(org, CurrentUser.GetUserId(User), body, CurrentUser.GetActor(User), ct);
        SubscriptionGateFilter.Evict(_cache, org);
        return Ok(Envelope.Ok(result));
    }

    [HttpPost("cancel")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<SubscriptionResponse>>> Cancel(
        [FromServices] IUserRepository users, [FromServices] IUserShopAccessRepository access, CancellationToken ct)
    {
        var org = CurrentUser.GetOrganizationId(User);
        await Application.Admin.BranchAuthority.RequireOrganizationAdminAsync(users, access, CurrentUser.GetUserId(User), org, ct);
        var result = await _subscriptions.CancelAsync(org, CurrentUser.GetUserId(User), CurrentUser.GetActor(User), ct);
        SubscriptionGateFilter.Evict(_cache, org);
        return Ok(Envelope.Ok(result));
    }

    /// <summary>
    /// Mercado Pago subscription notifications (signed with x-signature; the state is fetched from Mercado Pago).
    /// Each country's account points its webhook to /webhooks/mercadopago/{country}; the bare URL is Argentina's.
    /// </summary>
    [HttpPost("webhooks/mercadopago")]
    [HttpPost("webhooks/mercadopago/{country:length(2)}")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Webhooks)]
    public async Task<IActionResult> MercadoPago(string? country, CancellationToken ct)
    {
        var (type, dataId) = await WebhookPayload.ReadMercadoPagoAsync(Request, ct);
        var notification = new MercadoPagoNotification(type, dataId, Request.Headers["x-request-id"].FirstOrDefault(), Request.Headers["x-signature"].FirstOrDefault());
        return Done(await _subscriptions.HandleMercadoPagoWebhookAsync(country ?? "AR", notification, ct));
    }

    /// <summary>
    /// Country of the visitor as Cloudflare reports it (CF-IPCountry), so the website asks for cookie consent
    /// before loading ad tags in the EU. Null when unknown; the website then guesses from the time zone.
    /// </summary>
    private string? VisitorCountry()
    {
        var c = Request.Headers["CF-IPCountry"].ToString().Trim().ToUpperInvariant();
        return c.Length == 2 && c.All(char.IsAsciiLetterUpper) && c is not "XX" ? c : null;
    }

    private IActionResult Done(WebhookOutcome outcome)
    {
        if (outcome.OrganizationId is { } org) SubscriptionGateFilter.Evict(_cache, org);
        return outcome.Accepted ? Ok() : Unauthorized();
    }
}

public sealed record BillingConfigResponse(
    bool SignupEnabled,
    int TrialDays,
    IReadOnlyList<BillingCountryResponse> Countries,
    string? ContactEmail,
    string? LegalName,
    TrackingConfigResponse Tracking,
    string? VisitorCountry);

/// <summary>A country where shops can sign up and pay, and the currency Mercado Pago charges there.</summary>
public sealed record BillingCountryResponse(string Code, string Currency);

/// <summary>Ad measurement tags the website loads (each null when not configured).</summary>
public sealed record TrackingConfigResponse(string? Ga4Id, string? GoogleAdsId, string? GoogleAdsSignupLabel, string? GoogleAdsPurchaseLabel, string? MetaPixelId);
