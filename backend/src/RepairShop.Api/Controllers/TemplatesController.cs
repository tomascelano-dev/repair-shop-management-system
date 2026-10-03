using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairShop.Api.Common;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Messaging;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Authorize]
public sealed class TemplatesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<MessageTemplateResponse>>>> List(
        [FromServices] IMessageTemplateRepository repo,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var list = await repo.ListAsync(shopId, includeInactive, ct);
        var res = list.Select(ToResponse).ToList();
        return Ok(new ApiResponse<List<MessageTemplateResponse>>(res));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MessageTemplateResponse>>> GetById(
        [FromServices] IMessageTemplateRepository repo,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var t = await repo.GetByIdAsync(shopId, id, ct);
        if (t is null) return NotFound();

        return Ok(new ApiResponse<MessageTemplateResponse>(ToResponse(t)));
    }

    [HttpGet("by-key/{key}")]
    public async Task<ActionResult<ApiResponse<MessageTemplateResponse>>> GetByKey(
        [FromServices] IMessageTemplateRepository repo,
        string key,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var t = await repo.GetByKeyAsync(shopId, key, ct);
        if (t is null) return NotFound();
        return Ok(new ApiResponse<MessageTemplateResponse>(ToResponse(t)));
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<MessageTemplateResponse>>> Create(
        [FromServices] IMessageTemplateRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        [FromBody] CreateMessageTemplateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var template = new MessageTemplate(shopId, body.Key, body.Title, body.Body, body.IsActive, clock.UtcNow);
        await repo.AddAsync(template, ct);
        await uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = template.Id }, new ApiResponse<MessageTemplateResponse>(ToResponse(template)));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<MessageTemplateResponse>>> Update(
        [FromServices] IMessageTemplateRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        [FromBody] UpdateMessageTemplateRequest body,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var t = await repo.GetByIdAsync(shopId, id, ct);
        if (t is null) return NotFound();

        t.Update(body.Title, body.Body, body.IsActive, clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        return Ok(new ApiResponse<MessageTemplateResponse>(ToResponse(t)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(
        [FromServices] IMessageTemplateRepository repo,
        [FromServices] IUnitOfWork uow,
        Guid id,
        CancellationToken ct)
    {
        var shopId = CurrentUser.GetShopId(User);
        if (shopId == Guid.Empty) return Unauthorized();

        var t = await repo.GetByIdAsync(shopId, id, ct);
        if (t is null) return NotFound();

        await repo.RemoveAsync(t, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Placeholders available in templates (for the editor).</summary>
    [HttpGet("tokens")]
    public ActionResult<ApiResponse<IReadOnlyList<TemplateTokenResponse>>> Tokens()
        => Ok(Envelope.Ok(TemplateTokens));

    /// <summary>Restores the original text of a built-in template.</summary>
    [HttpPost("{id:guid}/restore-default")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<MessageTemplateResponse>>> RestoreDefault(
        [FromServices] IMessageTemplateRepository repo,
        [FromServices] IUnitOfWork uow,
        [FromServices] IDateTimeProvider clock,
        Guid id,
        CancellationToken ct)
    {
        var t = await repo.GetByIdAsync(CurrentUser.GetShopId(User), id, ct);
        if (t is null) return NotFound();

        var original = DbSeeder.DefaultTemplates.FirstOrDefault(x => x.Key == t.Key);
        if (original.Key is null) throw new RepairShop.Domain.Common.DomainException("Esta plantilla no tiene un texto original para restaurar.");

        t.Update(original.Title, original.Body, true, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Ok(Envelope.Ok(ToResponse(t)));
    }

    public sealed record TemplateTokenResponse(string Token, string Description);

    private static readonly IReadOnlyList<TemplateTokenResponse> TemplateTokens = new TemplateTokenResponse[]
    {
        new("customer_name", "Nombre completo del cliente"),
        new("customer_first_name", "Nombre de pila del cliente"),
        new("customer_phone", "Teléfono del cliente"),
        new("device_brand", "Marca del equipo"),
        new("device_model", "Modelo del equipo"),
        new("device_label", "Etiqueta del equipo (color, capacidad…)"),
        new("device_serial", "Número de serie"),
        new("device_imei", "IMEI"),
        new("issue_description", "Falla reportada"),
        new("order_code", "Número de orden (#000123)"),
        new("order_status_label", "Estado actual de la orden"),
        new("order_total", "Total de la orden"),
        new("paid_total", "Total pagado"),
        new("balance_due", "Saldo pendiente"),
        new("promised_date", "Fecha prometida de entrega"),
        new("quote_amount", "Total del presupuesto"),
        new("quote_currency", "Moneda del presupuesto"),
        new("quote_items", "Detalle de ítems del presupuesto"),
        new("quote_valid_until", "Vencimiento del presupuesto"),
        new("warranty_days", "Días de garantía"),
        new("warranty_expires_at", "Vencimiento de la garantía"),
        new("cancellation_reason", "Motivo de cancelación"),
        new("technician_name", "Técnico asignado"),
        new("tracking_url", "Link de seguimiento para el cliente"),
        new("feedback_url", "Link de la encuesta de satisfacción"),
        new("google_review_url", "Link de reseñas de Google"),
        new("shop_name", "Nombre de la sucursal"),
        new("shop_phone", "Teléfono de la sucursal"),
        new("shop_address", "Dirección de la sucursal"),
        new("pickup_address", "Dirección de retiro"),
        new("pickup_hours", "Horario de retiro"),
    };

    private static MessageTemplateResponse ToResponse(MessageTemplate t)
        => new(t.Id, t.ShopId, t.Key, t.Title, t.Body, t.IsActive, t.CreatedAtUtc, t.UpdatedAtUtc);
}
