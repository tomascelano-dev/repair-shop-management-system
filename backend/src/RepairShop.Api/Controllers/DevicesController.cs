using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Customers;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize]
public sealed class DevicesController : ControllerBase
{
    private readonly DeviceService _devices;

    public DevicesController(DeviceService devices) => _devices = devices;

    /// <summary>Search by brand, model, serial, IMEI or customer. Paged with X-Total-Count.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DeviceResponse>>>> List(
        [FromQuery] string? q, [FromQuery] Guid? customerId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (customerId is not null)
        {
            var items = await _devices.ListByCustomerAsync(shopId, customerId.Value, skip, take, ct);
            Response.Headers[HttpExtensions.TotalCountHeader] = items.Count.ToString();
            return Ok(Envelope.Ok<IReadOnlyList<DeviceResponse>>(items));
        }

        return Ok(Envelope.Ok(Response.WithTotal(await _devices.SearchAsync(shopId, q, skip, take, ct))));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DeviceResponse>>> Get(Guid id, CancellationToken ct)
        => Ok(Envelope.Ok(await _devices.GetAsync(CurrentUser.GetShopId(User), id, ct)));

    [HttpPost]
    [Authorize(Policy = Policies.OrdersManage)]
    public async Task<ActionResult<ApiResponse<DeviceResponse>>> Create([FromBody] DeviceCreateRequest body, CancellationToken ct)
    {
        var created = await _devices.CreateAsync(CurrentUser.GetShopId(User), body, CurrentUser.GetActor(User), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, Envelope.Ok(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OrdersManage)]
    public async Task<ActionResult<ApiResponse<DeviceResponse>>> Update(Guid id, [FromBody] DeviceUpdateRequest body, CancellationToken ct)
        => Ok(Envelope.Ok(await _devices.UpdateAsync(CurrentUser.GetShopId(User), id, body, CurrentUser.GetActor(User), ct)));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _devices.DeleteAsync(CurrentUser.GetShopId(User), id, CurrentUser.GetActor(User), ct);
        return NoContent();
    }
}
