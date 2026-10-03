using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Admin;
using RepairShop.Application.Contracts;
using RepairShop.Application.Files;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/settings")]
[Authorize]
public sealed class SettingsController : ControllerBase
{
    private readonly ShopSettingsService _settings;

    public SettingsController(ShopSettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ShopSettingsResponse>>> Get(CancellationToken ct)
        => Ok(Envelope.Ok(await _settings.GetAsync(CurrentUser.GetShopId(User), ct)));

    [HttpPut]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<ShopSettingsResponse>>> Update([FromBody] UpdateShopSettingsRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _settings.UpdateAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpPost("logo")]
    [Authorize(Policy = Policies.AdminOnly)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ShopSettingsResponse>>> UploadLogo([FromServices] FileService files, IFormFile file, CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        await using var stream = file.OpenReadStream();
        var stored = await files.UploadAsync(shopId, stream, file.FileName, "logo", imagesOnly: true, CurrentUser.GetUserId(User), ct);
        return Ok(Envelope.Ok(await _settings.SetLogoAsync(shopId, stored.Id, CurrentUser.GetActor(User), ct)));
    }

    [HttpDelete("logo")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<ShopSettingsResponse>>> RemoveLogo(CancellationToken ct)
        => Ok(Envelope.Ok(await _settings.SetLogoAsync(CurrentUser.GetShopId(User), null, CurrentUser.GetActor(User), ct)));

    [HttpGet("integrations")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<IntegrationsStatusResponse>>> Integrations(CancellationToken ct)
        => Ok(Envelope.Ok(await _settings.GetIntegrationsAsync(CurrentUser.GetShopId(User), ct)));

    [HttpPut("integrations/mercadopago")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<IntegrationsStatusResponse>>> MercadoPago([FromBody] UpdateMercadoPagoRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _settings.UpdateMercadoPagoAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpPut("integrations/fiscal")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<IntegrationsStatusResponse>>> Fiscal([FromBody] UpdateFiscalRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _settings.UpdateFiscalAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpGet("branches")]
    public async Task<ActionResult<ApiResponse<List<BranchResponse>>>> Branches(CancellationToken ct)
        => Ok(Envelope.Ok(await _settings.ListBranchesAsync(CurrentUser.GetShopId(User), CurrentUser.GetUserId(User), ct)));

    [HttpPost("branches")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<BranchResponse>>> CreateBranch([FromBody] CreateBranchRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _settings.CreateBranchAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct)));

    [HttpPost("branches/{id:guid}/active")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> SetBranchActive(Guid id, [FromQuery] bool value, CancellationToken ct)
    {
        await _settings.SetBranchActiveAsync(CurrentUser.GetShopId(User), id, value, CurrentUser.GetActor(User), ct);
        return NoContent();
    }
}
