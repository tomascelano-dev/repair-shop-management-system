using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Application.Customers;
using RepairShop.Application.Imports;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly CustomerService _customers;

    public CustomersController(CustomerService customers) => _customers = customers;

    /// <summary>Search by name, phone (any format), email, document or tag. Paged with X-Total-Count.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerResponse>>>> List(
        [FromQuery] string? q, [FromQuery] string? tag, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] string? sortBy, [FromQuery] string? sortDir, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var page = await _customers.SearchAsync(CurrentUser.GetShopId(User),
            new CustomerSearchOptions(q, dateFrom?.ToUniversalTime(), dateTo?.ToUniversalTime(), sortBy, sortDir, skip, take, tag), ct);
        return Ok(Envelope.Ok(Response.WithTotal(page)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Get(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _customers.GetAsync(CurrentUser.GetShopId(User), id, ct)));

    /// <summary>Customer 360: devices, orders, sales, total spent, balance due and feedback.</summary>
    [HttpGet("{id:guid}/summary")]
    public async Task<ActionResult<ApiResponse<CustomerSummaryResponse>>> Summary(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _customers.GetSummaryAsync(CurrentUser.GetShopId(User), id, ct)));

    /// <summary>Possible duplicates (same phone, ignoring formatting) before creating a customer.</summary>
    [HttpGet("duplicates")]
    public async Task<ActionResult<ApiResponse<List<DuplicateCustomerResponse>>>> Duplicates([FromQuery] string phone, [FromQuery] Guid? excludeId, CancellationToken ct)
        => Ok(Envelope.Ok(await _customers.FindDuplicatesAsync(CurrentUser.GetShopId(User), phone, excludeId, ct)));

    [HttpPost]
    [Authorize(Policy = Policies.OrdersManage)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Create([FromBody] CustomerCreateRequest body, CancellationToken ct)
    {
        var created = await _customers.CreateAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, Envelope.Ok(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OrdersManage)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Update(Guid id, [FromBody] CustomerUpdateRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _customers.UpdateAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _customers.DeleteAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct);
        return NoContent();
    }

    /// <summary>Moves devices, orders, sales and feedback of the source customer into this one and deletes the source.</summary>
    [HttpPost("{id:guid}/merge")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Merge(Guid id, [FromBody] MergeCustomersRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _customers.MergeAsync(CurrentUser.GetShopId(User), id, body.SourceCustomerId, CurrentUser.GetActor(User), ct)));

    /// <summary>Imports customers from Excel (.xlsx) or CSV. Use dryRun=true to preview and validate first.</summary>
    [HttpPost("import")]
    [Authorize(Policy = Policies.AdminOnly)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ImportResult>>> Import([FromServices] ImportService imports, IFormFile file, [FromQuery] bool dryRun = true, CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        return Ok(Envelope.Ok(await imports.ImportCustomersAsync(CurrentUser.GetShopId(User), stream, file.FileName, dryRun, CurrentUser.GetActor(User), ct)));
    }

    [HttpGet("export")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Export([FromServices] ImportService imports, CancellationToken ct)
        => File(await imports.ExportCustomersAsync(CurrentUser.GetShopId(User), ct), XlsxContentType, $"clientes-{DateTime.UtcNow:yyyyMMdd}.xlsx");
}
