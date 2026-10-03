using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.Security;
using RepairShop.Api.V2;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Saas;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public sealed record AppointmentRequest(int Version, Guid? CustomerId, string CustomerName, string Phone, string? Email, string? DeviceLabel, string Reason,
    DateTime StartsAtUtc, int DurationMinutes, Guid? TechnicianId, string Kind, string? Address, int RecurrenceMonths);
public sealed record AppointmentStatusRequest(int Version, string Status);
public sealed record ConvertAppointmentRequest(int Version, string Brand, string Model, string? Identifier, string Issue, string? Condition, string? Accessories);
public sealed record PublicBookingRequest(string CustomerName, string Phone, string? Email, string? DeviceLabel, string Reason, DateTime StartsAtUtc);
public sealed record SurveyAnswer(int Score, string? Comment);
public sealed record SendPortalRequest(int Version);
public sealed record ContactRequest(string? Email, int DocType, string? DocNumber, string? TaxCondition, string? Address);

public static class AgendaRules
{
    public static readonly string[] Statuses = ["Booked", "Confirmed", "Done", "NoShow", "Cancelled", "Converted"];

    // Free slots for a local date, based on the shop's opening hours and existing appointments.
    public static List<DateTime> Slots(OpeningHours hours, DateOnly day, IEnumerable<(DateTime Start, int Minutes)> busy, DateTime nowUtc)
    {
        var result = new List<DateTime>();
        if (!hours.Days.Contains((int)day.DayOfWeek)) return result;
        var from = TimeOnly.Parse(hours.From, CultureInfo.InvariantCulture); var to = TimeOnly.Parse(hours.To, CultureInfo.InvariantCulture);
        var taken = busy.ToList();
        for (var t = from; t.AddMinutes(hours.SlotMinutes) <= to && t >= from; t = t.AddMinutes(hours.SlotMinutes))
        {
            var startUtc = DateTime.SpecifyKind(day.ToDateTime(t).AddHours(3), DateTimeKind.Utc); // Argentina UTC-3
            var endUtc = startUtc.AddMinutes(hours.SlotMinutes);
            if (startUtc <= nowUtc.AddMinutes(30)) continue;
            if (taken.Any(b => b.Start < endUtc && b.Start.AddMinutes(b.Minutes) > startUtc)) continue;
            result.Add(startUtc);
            if (t.AddMinutes(hours.SlotMinutes) < t) break;
        }
        return result;
    }
}

[ApiController, Authorize, Route("api/saas/appointments")]
public sealed class AppointmentsController(RepairShopDbContext db, Notifier notifier) : SaasController(db)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = from ?? DateTime.UtcNow.Date.AddDays(-7); var end = to ?? start.AddDays(60);
        if ((end - start).TotalDays > 400) throw new DomainException("Consultá hasta 400 días por vez.");
        var rows = await Own<Appointment>().Where(x => x.StartsAtUtc >= start && x.StartsAtUtc < end).OrderBy(x => x.StartsAtUtc).ToListAsync();
        var techs = await Db.Users.Where(x => x.ShopId == Shop).Select(x => new { x.Id, x.DisplayName, Role = x.Role.ToString() }).ToListAsync();
        var due = await Own<Appointment>().Where(x => x.RecurrenceMonths > 0 && x.Status == "Booked" && x.StartsAtUtc < DateTime.UtcNow.AddDays(30)).CountAsync();
        return Ok(new { data = new { appointments = rows, technicians = techs, preventiveDueSoon = due } });
    }

    private async Task Fill(Appointment a, AppointmentRequest b)
    {
        if (b.CustomerId is Guid cid)
        {
            var customer = await Db.Customers.SingleOrDefaultAsync(x => x.Id == cid && x.ShopId == Shop) ?? throw new DomainException("Cliente no disponible.");
            a.CustomerId = customer.Id;
        }
        else a.CustomerId = null;
        a.CustomerName = Saas.Text(b.CustomerName, "Cliente", 3, 120);
        a.Phone = Saas.Text(b.Phone, "Teléfono", 6, 40);
        a.Email = Saas.Email(b.Email);
        a.DeviceLabel = Saas.Text(b.DeviceLabel, "Equipo", 0, 120);
        a.Reason = Saas.Text(b.Reason, "Motivo", 3, 500);
        if (b.StartsAtUtc < DateTime.UtcNow.AddYears(-1) || b.StartsAtUtc > DateTime.UtcNow.AddYears(2)) throw new DomainException("Fecha fuera de rango.");
        a.StartsAtUtc = DateTime.SpecifyKind(b.StartsAtUtc, DateTimeKind.Utc);
        a.DurationMinutes = b.DurationMinutes is >= 10 and <= 600 ? b.DurationMinutes : throw new DomainException("La duración va de 10 a 600 minutos.");
        if (b.TechnicianId is Guid tid && !await Db.Users.AnyAsync(x => x.Id == tid && x.ShopId == Shop)) throw new DomainException("Técnico no encontrado.");
        a.TechnicianId = b.TechnicianId;
        a.Kind = b.Kind is "Workshop" or "Field" ? b.Kind : throw new DomainException("Tipo de turno inválido.");
        a.Address = Saas.Text(b.Address, "Dirección", a.Kind == "Field" ? 5 : 0, 250);
        a.RecurrenceMonths = b.RecurrenceMonths is >= 0 and <= 24 ? b.RecurrenceMonths : throw new DomainException("La recurrencia va de 0 a 24 meses.");
    }

    [HttpPost]
    public Task<IActionResult> Create(AppointmentRequest b) => Change("appointment", b, async () =>
    {
        var a = new Appointment { ShopId = Shop };
        await Fill(a, b);
        Db.Appointments.Add(a);
        var profile = await ShopProvisioning.EnsureProfile(Db, Shop);
        await notifier.Queue(Shop, a.Email, a.Phone, $"Turno confirmado en {profile.DisplayName}",
            $"Hola {a.CustomerName}, agendamos tu turno para el {Saas.When(SaasScheduler.Local(a.StartsAtUtc))}. Motivo: {a.Reason}.", $"appointment:{a.Id}:created", "appointment", a.Id, allowSms: false);
        Webhooks.Enqueue(Db, Shop, "appointment.created", new { a.Id, a.StartsAtUtc, a.CustomerName, a.Reason, a.Kind });
        return new { a.Id };
    });

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, AppointmentRequest b) => Change($"appointment:{id}", b, async () =>
    {
        var a = await Find<Appointment>(id); CheckVersion(a, b.Version);
        if (a.Status is "Converted" or "Done") throw new DomainException("El turno ya se cerró.");
        var moved = a.StartsAtUtc != b.StartsAtUtc;
        await Fill(a, b);
        if (moved) a.ReminderSentAtUtc = null;
        return new { a.Id, a.Version };
    });

    [HttpPost("{id:guid}/status")]
    public Task<IActionResult> Status(Guid id, AppointmentStatusRequest b) => Change($"appointment:status:{id}", b, async () =>
    {
        var a = await Find<Appointment>(id); CheckVersion(a, b.Version);
        if (!AgendaRules.Statuses.Contains(b.Status) || b.Status == "Converted") throw new DomainException("Estado inválido.");
        if (a.Status == "Converted") throw new DomainException("El turno ya se convirtió en orden.");
        a.Status = b.Status;
        Guid? next = null;
        // Preventive maintenance: completing a recurring visit books the next one.
        if (b.Status == "Done" && a.RecurrenceMonths > 0 && !await Own<Appointment>().AnyAsync(x => x.PreviousId == a.Id))
        {
            var follow = new Appointment { ShopId = Shop, CustomerId = a.CustomerId, CustomerName = a.CustomerName, Phone = a.Phone, Email = a.Email, DeviceLabel = a.DeviceLabel,
                Reason = a.Reason, StartsAtUtc = a.StartsAtUtc.AddMonths(a.RecurrenceMonths), DurationMinutes = a.DurationMinutes, TechnicianId = a.TechnicianId, Kind = a.Kind,
                Address = a.Address, RecurrenceMonths = a.RecurrenceMonths, PreviousId = a.Id };
            Db.Appointments.Add(follow); next = follow.Id;
        }
        return new { a.Id, a.Status, next };
    });

    [HttpPost("{id:guid}/convert")]
    public Task<IActionResult> Convert(Guid id, ConvertAppointmentRequest b) => Change($"appointment:convert:{id}", b, async () =>
    {
        var a = await Find<Appointment>(id); CheckVersion(a, b.Version);
        if (a.Status is "Converted" or "Cancelled") throw new DomainException("Este turno no se puede convertir.");
        var intake = new IntakeRequest(a.CustomerId, null, a.CustomerName, a.Phone, b.Brand, b.Model, b.Identifier, b.Issue, b.Condition ?? "", b.Accessories ?? "", "Normal", null, a.Email);
        var created = await WorkshopIntake.Create(Db, Shop, Actor, intake);
        a.Status = "Converted"; a.OrderId = created.Id;
        if (a.CustomerId is null) a.CustomerId = await Db.RepairOrders.Where(x => x.Id == created.Id).Select(x => x.CustomerId).SingleAsync();
        return new { orderId = created.Id, created.Number };
    });
}

// Customer-facing endpoints without login: tracking widget, online booking and satisfaction surveys.
[ApiController, AllowAnonymous, Route("api/public")]
public sealed class PublicController(RepairShopDbContext db, SubscriptionService subscriptions, Notifier notifier, LoginSecurityService limiter) : ControllerBase
{
    private async Task<ShopProfile> Profile(string slug) =>
        await db.ShopProfiles.SingleOrDefaultAsync(x => x.Slug == slug.ToLowerInvariant()) ?? throw new RepairShop.Application.Common.NotFoundException("Taller no encontrado.");

    private void Limit() => limiter.EnforceRateLimit("public:" + (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"));

    [HttpGet("shops/{slug}")]
    public async Task<IActionResult> Shop(string slug)
    {
        var p = await Profile(slug);
        var state = await subscriptions.GetAsync(p.ShopId);
        return Ok(new { data = new { p.Slug, p.DisplayName, p.LogoDataUrl, p.PrimaryColor, p.Address, p.City, p.Phone, p.Email, p.Website,
            OnlineBooking = p.OnlineBookingEnabled && state.Modules.Contains("agenda") && !state.ReadOnly, Hours = ShopProvisioning.Hours(p) } });
    }

    [HttpGet("shops/{slug}/track")]
    public async Task<IActionResult> Track(string slug, [FromQuery] string code)
    {
        Limit();
        var p = await Profile(slug);
        code = (code ?? "").Trim().ToUpperInvariant().Replace("OT-", "");
        OrderTracking? t = null;
        if (code.Length == 8) t = await db.OrderTrackings.SingleOrDefaultAsync(x => x.ShopId == p.ShopId && x.Code == code);
        if (t is null) throw new RepairShop.Application.Common.NotFoundException("No encontramos una orden con ese código. Revisá el comprobante.");
        var w = await db.Workflows.SingleAsync(x => x.Id == t.OrderId && x.ShopId == p.ShopId);
        var o = await db.RepairOrders.SingleAsync(x => x.Id == t.OrderId && x.ShopId == p.ShopId);
        var history = await db.RepairOrderStatusHistory.Where(x => x.RepairOrderId == o.Id && x.ShopId == p.ShopId).OrderBy(x => x.ChangedAtUtc).Select(x => new { Status = x.ToStatus.ToString(), x.ChangedAtUtc }).ToListAsync();
        var quote = await db.WorkflowQuotes.Where(x => x.OrderId == o.Id && x.ShopId == p.ShopId).OrderByDescending(x => x.Revision).Select(x => new { x.Total, x.Currency, x.Status }).FirstOrDefaultAsync();
        var first = w.CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return Ok(new { data = new { Number = SaasEvents.Code(w.Number), Customer = first, w.DeviceLabel, Status = o.Status.ToString(), o.CreatedAtUtc, o.UpdatedAtUtc, w.HandedOverAtUtc, history, quote } });
    }

    [HttpGet("shops/{slug}/slots")]
    public async Task<IActionResult> Slots(string slug, [FromQuery] DateOnly date)
    {
        var p = await Profile(slug);
        if (!p.OnlineBookingEnabled) throw new RepairShop.Application.Common.NotFoundException("Este taller no toma turnos online.");
        if (date < DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) || date > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90))) return Ok(new { data = Array.Empty<DateTime>() });
        var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(3), DateTimeKind.Utc);
        var busy = await db.Appointments.Where(x => x.ShopId == p.ShopId && x.StartsAtUtc >= dayStart.AddHours(-12) && x.StartsAtUtc < dayStart.AddHours(36) && x.Status != "Cancelled")
            .Select(x => new { x.StartsAtUtc, x.DurationMinutes }).ToListAsync();
        return Ok(new { data = AgendaRules.Slots(ShopProvisioning.Hours(p), date, busy.Select(x => (x.StartsAtUtc, x.DurationMinutes)), DateTime.UtcNow) });
    }

    [HttpPost("shops/{slug}/appointments")]
    public async Task<IActionResult> Book(string slug, PublicBookingRequest b)
    {
        Limit();
        var p = await Profile(slug);
        var state = await subscriptions.GetAsync(p.ShopId);
        if (!p.OnlineBookingEnabled || !state.Modules.Contains("agenda") || state.ReadOnly) throw new RepairShop.Application.Common.NotFoundException("Este taller no toma turnos online.");
        var start = DateTime.SpecifyKind(b.StartsAtUtc, DateTimeKind.Utc);
        var local = SaasScheduler.Local(start);
        await using var tx = await db.Database.BeginTransactionAsync();
        var lockName = $"premium:{p.ShopId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockName}, 0))");
        var dayStart = start.AddDays(-1);
        var busy = await db.Appointments.Where(x => x.ShopId == p.ShopId && x.StartsAtUtc >= dayStart && x.StartsAtUtc < start.AddDays(1) && x.Status != "Cancelled").Select(x => new { x.StartsAtUtc, x.DurationMinutes }).ToListAsync();
        if (!AgendaRules.Slots(ShopProvisioning.Hours(p), DateOnly.FromDateTime(local), busy.Select(x => (x.StartsAtUtc, x.DurationMinutes)), DateTime.UtcNow).Contains(start))
            throw new WorkflowConflict("Ese horario ya no está disponible. Elegí otro.");
        var a = new Appointment { ShopId = p.ShopId, CustomerName = Saas.Text(b.CustomerName, "Nombre", 3, 120), Phone = Saas.Text(b.Phone, "Teléfono", 6, 40), Email = Saas.Email(b.Email),
            DeviceLabel = Saas.Text(b.DeviceLabel, "Equipo", 0, 120), Reason = Saas.Text(b.Reason, "Motivo", 3, 500), StartsAtUtc = start, DurationMinutes = ShopProvisioning.Hours(p).SlotMinutes, Source = "Online" };
        db.Appointments.Add(a);
        db.ShopAlerts.Add(new ShopAlert { ShopId = p.ShopId, Kind = "Booking", Message = $"Nuevo turno online: {a.CustomerName}, {Saas.When(local, false)}.", Link = "/agenda" });
        await notifier.Queue(p.ShopId, a.Email, a.Phone, $"Turno reservado en {p.DisplayName}", $"Hola {a.CustomerName}, reservaste un turno para el {Saas.When(local)} en {p.DisplayName}{(p.Address.Length > 0 ? $", {p.Address}" : "")}.", $"appointment:{a.Id}:created", "appointment", a.Id, allowSms: false);
        Webhooks.Enqueue(db, p.ShopId, "appointment.created", new { a.Id, a.StartsAtUtc, a.CustomerName, a.Reason, source = "Online" });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(new { data = new { a.Id, a.StartsAtUtc } });
    }

    [HttpGet("surveys/{token}")]
    public async Task<IActionResult> Survey(string token)
    {
        var s = await db.SatisfactionSurveys.SingleOrDefaultAsync(x => x.TokenHash == Saas.Sha256(token)) ?? throw new RepairShop.Application.Common.NotFoundException("Encuesta no encontrada.");
        var p = await ShopProvisioning.EnsureProfile(db, s.ShopId);
        var device = await db.Workflows.Where(x => x.Id == s.OrderId).Select(x => x.DeviceLabel).SingleAsync();
        return Ok(new { data = new { ShopName = p.DisplayName, p.LogoDataUrl, p.PrimaryColor, Device = device, Answered = s.AnsweredAtUtc != null, s.Score } });
    }

    [HttpPost("surveys/{token}")]
    public async Task<IActionResult> Answer(string token, SurveyAnswer b)
    {
        Limit();
        var s = await db.SatisfactionSurveys.SingleOrDefaultAsync(x => x.TokenHash == Saas.Sha256(token)) ?? throw new RepairShop.Application.Common.NotFoundException("Encuesta no encontrada.");
        if (s.AnsweredAtUtc is not null) throw new DomainException("Ya respondiste esta encuesta. ¡Gracias!");
        if (b.Score is < 0 or > 10) throw new DomainException("Elegí un puntaje de 0 a 10.");
        s.Score = b.Score; s.Comment = Saas.Text(b.Comment, "Comentario", 0, 1000); s.AnsweredAtUtc = DateTime.UtcNow; s.Version++;
        var number = await db.Workflows.Where(x => x.Id == s.OrderId).Select(x => x.Number).SingleAsync();
        db.ShopAlerts.Add(new ShopAlert { ShopId = s.ShopId, Kind = "Survey", Message = $"Encuesta de {SaasEvents.Code(number)}: {b.Score}/10{(s.Comment.Length > 0 ? $" · \"{(s.Comment.Length > 80 ? s.Comment[..80] + "…" : s.Comment)}\"" : "")}", OrderId = s.OrderId, Link = $"/orders/{s.OrderId}" });
        Webhooks.Enqueue(db, s.ShopId, "survey.answered", new { s.OrderId, s.Score, s.Comment });
        await db.SaveChangesAsync();
        return Ok(new { data = new { ok = true } });
    }
}

// Order-level extras used by the order detail screen: tracking code, customer contact and sending the approval link.
[ApiController, Authorize, Route("api/saas/orders")]
public sealed class OrderExtrasController(RepairShopDbContext db, Notifier notifier) : SaasController(db)
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var order = await Db.RepairOrders.Where(x => x.Id == id && x.ShopId == Shop).Select(x => new { x.Id, x.CustomerId }).SingleOrDefaultAsync()
            ?? throw new RepairShop.Application.Common.NotFoundException("Orden no encontrada.");
        var tracking = await Own<OrderTracking>().SingleOrDefaultAsync(x => x.OrderId == id);
        if (tracking is null) { tracking = await notifier.EnsureTracking(Shop, id); await Db.SaveChangesAsync(); }
        var profile = await ShopProvisioning.EnsureProfile(Db, Shop);
        var contact = await Own<CustomerContact>().SingleOrDefaultAsync(x => x.CustomerId == order.CustomerId);
        var signatures = await Own<PortalSignature>().Where(x => x.OrderId == id).OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.QuoteId, x.SignerName, x.Accepted, x.SignatureDataUrl, x.CreatedAtUtc, x.IpAddress }).ToListAsync();
        var survey = await Own<SatisfactionSurvey>().Where(x => x.OrderId == id).Select(x => new { x.Score, x.Comment, x.AnsweredAtUtc, x.CreatedAtUtc }).SingleOrDefaultAsync();
        var invoices = await Own<FiscalInvoice>().Where(x => x.SourceId == id).OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.VoucherType, x.PointOfSale, x.Number, x.Status, x.Total, x.Currency, x.CancelsInvoiceId, x.Cae }).ToListAsync();
        var assignment = await Own<OrderAssignment>().SingleOrDefaultAsync(x => x.OrderId == id);
        var messages = await Db.NotificationOutbox.Where(x => x.ShopId == Shop && x.RelatedEntityId == id).OrderByDescending(x => x.CreatedAtUtc).Take(20)
            .Select(x => new { x.Id, Channel = x.Channel.ToString(), x.Recipient, x.Title, Status = x.Status.ToString(), x.CreatedAtUtc, x.LastError }).ToListAsync();
        return Ok(new { data = new { tracking.Code, TrackUrl = notifier.TrackUrl(profile.Slug, tracking.Code), Email = tracking.CustomerEmail.Length > 0 ? tracking.CustomerEmail : contact?.Email ?? "",
            contact, signatures, survey, invoices, assignment, messages, profile.RequireSignature } });
    }

    [HttpPut("{id:guid}/contact")]
    public Task<IActionResult> Contact(Guid id, ContactRequest b) => Change($"order:contact:{id}", b, async () =>
    {
        var customerId = await Db.RepairOrders.Where(x => x.Id == id && x.ShopId == Shop).Select(x => (Guid?)x.CustomerId).SingleOrDefaultAsync() ?? throw new RepairShop.Application.Common.NotFoundException("Orden no encontrada.");
        var contact = await Own<CustomerContact>().SingleOrDefaultAsync(x => x.CustomerId == customerId);
        if (contact is null) { contact = new CustomerContact { ShopId = Shop, CustomerId = customerId }; Db.CustomerContacts.Add(contact); }
        contact.Email = Saas.Email(b.Email);
        contact.DocType = b.DocType is 80 or 86 or 96 or 99 ? b.DocType : throw new DomainException("Tipo de documento inválido.");
        contact.DocNumber = new string((b.DocNumber ?? "").Where(char.IsDigit).ToArray());
        contact.TaxCondition = b.TaxCondition is "ConsumidorFinal" or "ResponsableInscripto" or "Monotributo" or "Exento" ? b.TaxCondition : "ConsumidorFinal";
        contact.Address = Saas.Text(b.Address, "Dirección", 0, 250);
        var tracking = await notifier.EnsureTracking(Shop, id);
        tracking.CustomerEmail = contact.Email;
        return new { contact.Id };
    });

    // Creates a fresh approval link (revoking the previous one, as in the workshop command) and emails/texts it to the customer.
    [HttpPost("{id:guid}/send-portal")]
    public Task<IActionResult> SendPortal(Guid id, SendPortalRequest b) => Change($"order:send-portal:{id}", b, async () =>
    {
        var (w, o) = await LockOrder(id);
        if (w.Version != b.Version) throw new WorkflowConflict("La orden cambió. Actualizá la pantalla e intentá nuevamente.");
        var token = Saas.Token();
        w.PortalTokenHash = Saas.Sha256(token); w.PortalExpiresAtUtc = DateTime.UtcNow.AddDays(30); w.Version++;
        var tracking = await notifier.EnsureTracking(Shop, id);
        var email = tracking.CustomerEmail.Length > 0 ? tracking.CustomerEmail : await Own<CustomerContact>().Where(x => x.CustomerId == o.CustomerId).Select(x => x.Email).SingleOrDefaultAsync() ?? "";
        var profile = await ShopProvisioning.EnsureProfile(Db, Shop);
        var quote = await Db.WorkflowQuotes.Where(x => x.ShopId == Shop && x.OrderId == id).OrderByDescending(x => x.Revision).FirstOrDefaultAsync();
        var link = $"{notifier.AppUrl}/portal#{token}";
        await notifier.Queue(Shop, email, w.CustomerPhone, quote?.Status == "Sent" ? $"Presupuesto de tu {w.DeviceLabel}" : $"Estado de tu {w.DeviceLabel}",
            quote?.Status == "Sent"
                ? $"Hola {w.CustomerName}, el presupuesto de tu {w.DeviceLabel} (orden {SaasEvents.Code(w.Number)}) es de {quote.Currency} {quote.Total:N2}. Revisalo y aprobalo acá: {link}"
                : $"Hola {w.CustomerName}, podés ver el estado de tu {w.DeviceLabel} (orden {SaasEvents.Code(w.Number)}) acá: {link}",
            $"order:{id}:portal:{w.Version}", "order", id);
        Note(id, "Enlace del portal enviado al cliente; los anteriores quedan revocados.");
        return new { token, w.Version, sentTo = new[] { email, w.CustomerPhone }.Where(x => x.Length > 0) };
    });
}
