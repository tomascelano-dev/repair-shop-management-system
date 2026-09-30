using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Devices;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize(Policy = Policies.StaffOnly)]
public sealed class DevicesController : ControllerBase
{
    [HttpGet("by-customer/{customerId:guid}")]
    public async Task<ActionResult<ApiResponse<List<DeviceResponse>>>> ListByCustomer(
        [FromServices] IDeviceRepository repo,
        Guid customerId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var list = await repo.ListByCustomerAsync(shopId, customerId, skip, take, ct);
        var res = list.Select(d => new DeviceResponse(d.Id, d.ShopId, d.CustomerId, d.Brand, d.Model, d.Label, d.SerialNumber, d.Notes, d.CreatedAtUtc)).ToList();
        return Ok(new ApiResponse<List<DeviceResponse>>(res));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DeviceResponse>>> GetById(
        [FromServices] IDeviceRepository repo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var d = await repo.GetByIdAsync(shopId, id, ct);
        if (d is null) return NotFound();

        var res = new DeviceResponse(d.Id, d.ShopId, d.CustomerId, d.Brand, d.Model, d.Label, d.SerialNumber, d.Notes, d.CreatedAtUtc);
        return Ok(new ApiResponse<DeviceResponse>(res));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DeviceResponse>>> Create(
        [FromServices] IDeviceRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        [FromBody] DeviceCreateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var device = new Device(shopId, body.CustomerId, body.Brand, body.Model, body.Label, body.SerialNumber, body.Notes, clock.UtcNow);
        await repo.AddAsync(device, ct);
        await uow.SaveChangesAsync(ct);

        var res = new DeviceResponse(device.Id, device.ShopId, device.CustomerId, device.Brand, device.Model, device.Label, device.SerialNumber, device.Notes, device.CreatedAtUtc);
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, new ApiResponse<DeviceResponse>(res));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DeviceResponse>>> Update(
        [FromServices] IDeviceRepository repo,
        [FromServices] IUnitOfWork uow,
        Guid id,
        [FromBody] DeviceUpdateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var device = await repo.GetByIdAsync(shopId, id, ct);
        if (device is null) return NotFound();

        device.Update(body.Brand, body.Model, body.Label, body.SerialNumber, body.Notes);
        await uow.SaveChangesAsync(ct);

        var res = new DeviceResponse(device.Id, device.ShopId, device.CustomerId, device.Brand, device.Model, device.Label, device.SerialNumber, device.Notes, device.CreatedAtUtc);
        return Ok(new ApiResponse<DeviceResponse>(res));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(
        [FromServices] IDeviceRepository repo,
        [FromServices] IUnitOfWork uow,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var device = await repo.GetByIdAsync(shopId, id, ct);
        if (device is null) return NotFound();

        await repo.RemoveAsync(device, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
