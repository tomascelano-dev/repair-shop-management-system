using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Customers;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize(Policy = Policies.StaffOnly)]
public sealed class CustomersController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CustomerResponse>>>> List(
        [FromServices] ICustomerRepository repo,
        [FromQuery] string? q = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var (items, total) = await repo.SearchAsync(shopId, new CustomerSearchOptions(
            Q: q,
            DateFromUtc: dateFrom?.ToUniversalTime(),
            DateToUtc: dateTo?.ToUniversalTime(),
            SortBy: sortBy,
            SortDir: sortDir,
            Skip: skip,
            Take: take
        ), ct);

        Response.Headers["X-Total-Count"] = total.ToString();

        var res = items.Select(c => new CustomerResponse(c.Id, c.ShopId, c.FullName, c.Phone, c.Notes, c.CreatedAtUtc)).ToList();
        return Ok(new ApiResponse<List<CustomerResponse>>(res));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> GetById(
        [FromServices] ICustomerRepository repo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var c = await repo.GetByIdAsync(shopId, id, ct);
        if (c is null) return NotFound();

        var res = new CustomerResponse(c.Id, c.ShopId, c.FullName, c.Phone, c.Notes, c.CreatedAtUtc);
        return Ok(new ApiResponse<CustomerResponse>(res));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Create(
        [FromServices] ICustomerRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        [FromBody] CustomerCreateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var customer = new Customer(shopId, body.FullName, body.Phone, body.Notes, clock.UtcNow);
        await repo.AddAsync(customer, ct);
        await uow.SaveChangesAsync(ct);

        var res = new CustomerResponse(customer.Id, customer.ShopId, customer.FullName, customer.Phone, customer.Notes, customer.CreatedAtUtc);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, new ApiResponse<CustomerResponse>(res));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Update(
        [FromServices] ICustomerRepository repo,
        [FromServices] IUnitOfWork uow,
        Guid id,
        [FromBody] CustomerUpdateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var customer = await repo.GetByIdAsync(shopId, id, ct);
        if (customer is null) return NotFound();

        customer.Update(body.FullName, body.Phone, body.Notes);
        await uow.SaveChangesAsync(ct);

        var res = new CustomerResponse(customer.Id, customer.ShopId, customer.FullName, customer.Phone, customer.Notes, customer.CreatedAtUtc);
        return Ok(new ApiResponse<CustomerResponse>(res));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(
        [FromServices] ICustomerRepository repo,
        [FromServices] IUnitOfWork uow,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var customer = await repo.GetByIdAsync(shopId, id, ct);
        if (customer is null) return NotFound();

        await repo.RemoveAsync(customer, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
