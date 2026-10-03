using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Application.Imports;
using RepairShop.Application.Inventory;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/inventory")]
[Authorize]
public sealed class InventoryController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly InventoryService _inventory;

    public InventoryController(InventoryService inventory) => _inventory = inventory;

    /// <summary>Search by SKU, name, barcode or category. Optional filters: sellable, low stock, compatible model.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InventoryItemResponse>>>> List(
        [FromQuery] string? q,
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool? onlySellable = null,
        [FromQuery] bool? onlyLowStock = null,
        [FromQuery] string? category = null,
        [FromQuery] string? brand = null,
        [FromQuery] string? model = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var options = new InventorySearchOptions(q, includeInactive, null, null, sortBy, sortDir, skip, take, onlySellable, onlyLowStock, category, brand, model);
        return Ok(Envelope.Ok(Response.WithTotal(await _inventory.SearchAsync(CurrentUser.GetShopId(User), options, ct))));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> Get(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _inventory.GetAsync(CurrentUser.GetShopId(User), id, ct)));

    /// <summary>Exact lookup by barcode or SKU (barcode scanner / camera).</summary>
    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> ByCode(string code, CancellationToken ct)
        => Ok(Envelope.Ok(await _inventory.FindByCodeAsync(CurrentUser.GetShopId(User), code, ct)));

    [HttpPost]
    [Authorize(Policy = Policies.InventoryManage)]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> Create([FromBody] CreateInventoryItemRequest body, CancellationToken ct)
    {
        var created = await _inventory.CreateAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, Envelope.Ok(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.InventoryManage)]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> Update(Guid id, [FromBody] UpdateInventoryItemRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _inventory.UpdateAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    /// <summary>Manual stock movement (purchase, count correction, loss, return...). Concurrency-safe.</summary>
    [HttpPost("{id:guid}/adjustments")]
    [Authorize(Policy = Policies.InventoryManage)]
    [Idempotent]
    public async Task<ActionResult<ApiResponse<InventoryItemResponse>>> Adjust(Guid id, [FromBody] CreateInventoryAdjustmentRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _inventory.AddAdjustmentAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    [HttpGet("{id:guid}/adjustments")]
    public async Task<ActionResult<ApiResponse<List<InventoryAdjustmentResponse>>>> Adjustments(Guid id, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(Envelope.Ok(await _inventory.ListAdjustmentsAsync(CurrentUser.GetShopId(User), id, skip, take, ct)));

    // ===== Compatible models (part ↔ device) =====

    [HttpGet("{id:guid}/compatibility")]
    public async Task<ActionResult<ApiResponse<List<CompatibilityResponse>>>> Compatibility(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _inventory.ListCompatibilityAsync(CurrentUser.GetShopId(User), id, ct)));

    [HttpPost("{id:guid}/compatibility")]
    [Authorize(Policy = Policies.InventoryManage)]
    public async Task<ActionResult<ApiResponse<CompatibilityResponse>>> AddCompatibility(Guid id, [FromBody] CompatibilityRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _inventory.AddCompatibilityAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    [HttpDelete("{id:guid}/compatibility/{compatibilityId:guid}")]
    [Authorize(Policy = Policies.InventoryManage)]
    public async Task<IActionResult> RemoveCompatibility(Guid id, Guid compatibilityId, CancellationToken ct)
    {
        await _inventory.RemoveCompatibilityAsync(CurrentUser.GetShopId(User), id, compatibilityId, ct);
        return NoContent();
    }

    // ===== Import / export =====

    [HttpPost("import")]
    [Authorize(Policy = Policies.InventoryManage)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ImportResult>>> Import([FromServices] ImportService imports, IFormFile file,
        [FromQuery] bool dryRun = true, [FromQuery] bool updateExisting = false, CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        return Ok(Envelope.Ok(await imports.ImportInventoryAsync(CurrentUser.GetShopId(User), stream, file.FileName, dryRun, updateExisting, CurrentUser.GetActor(User), ct)));
    }

    [HttpGet("export")]
    [Authorize(Policy = Policies.InventoryManage)]
    public async Task<IActionResult> Export([FromServices] ImportService imports, CancellationToken ct)
        => File(await imports.ExportInventoryAsync(CurrentUser.GetShopId(User), ct), XlsxContentType, $"inventario-{DateTime.UtcNow:yyyyMMdd}.xlsx");
}
