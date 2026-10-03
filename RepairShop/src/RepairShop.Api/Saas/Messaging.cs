using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Saas;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public sealed class MessagingOptions
{
    public const string Section = "Saas:Messaging";
    // Log (development: writes to the server log) | Live (SMTP + SMS provider)
    public string Provider { get; set; } = "Log";
    public SmtpSettings Smtp { get; set; } = new();
    public SmsSettings Sms { get; set; } = new();
}

public sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "RepairShop";
}

public sealed class SmsSettings
{
    // Twilio-compatible REST API.
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string From { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "https://api.twilio.com";
}

public sealed record OutgoingMessage(NotificationChannel Channel, string Recipient, string Subject, string Body, string ShopName, string ReplyTo, string Color);

public interface IMessageSender
{
    Task SendAsync(OutgoingMessage message, CancellationToken ct);
}

public sealed class LogMessageSender(ILogger<LogMessageSender> log) : IMessageSender
{
    public Task SendAsync(OutgoingMessage m, CancellationToken ct)
    {
        log.LogInformation("[{Channel}] to {Recipient}: {Subject} · {Body}", m.Channel, m.Recipient, m.Subject, m.Body);
        return Task.CompletedTask;
    }
}

public sealed class LiveMessageSender(HttpClient http, IOptions<MessagingOptions> options) : IMessageSender
{
    public async Task SendAsync(OutgoingMessage m, CancellationToken ct)
    {
        if (m.Channel == NotificationChannel.Email) await SendEmail(m, ct);
        else if (m.Channel == NotificationChannel.Sms) await SendSms(m, ct);
        else throw new InvalidOperationException("Canal no soportado.");
    }

    private async Task SendEmail(OutgoingMessage m, CancellationToken ct)
    {
        var s = options.Value.Smtp;
        if (string.IsNullOrWhiteSpace(s.Host) || string.IsNullOrWhiteSpace(s.From)) throw new InvalidOperationException("SMTP no configurado (Saas:Messaging:Smtp).");
        using var mail = new MailMessage { From = new MailAddress(s.From, m.ShopName.Length > 0 ? m.ShopName : s.FromName), Subject = m.Subject, Body = EmailHtml.Render(m), IsBodyHtml = true, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8 };
        mail.To.Add(m.Recipient);
        if (MailAddress.TryCreate(m.ReplyTo, out var reply)) mail.ReplyToList.Add(reply);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(m.Body, Encoding.UTF8, "text/plain"));
        using var client = new SmtpClient(s.Host, s.Port) { EnableSsl = s.EnableSsl, Credentials = string.IsNullOrEmpty(s.User) ? null : new NetworkCredential(s.User, s.Password) };
        await client.SendMailAsync(mail, ct);
    }

    private async Task SendSms(OutgoingMessage m, CancellationToken ct)
    {
        var s = options.Value.Sms;
        if (string.IsNullOrWhiteSpace(s.AccountSid) || string.IsNullOrWhiteSpace(s.AuthToken) || string.IsNullOrWhiteSpace(s.From)) throw new InvalidOperationException("SMS no configurado (Saas:Messaging:Sms).");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{s.ApiBaseUrl.TrimEnd('/')}/2010-04-01/Accounts/{Uri.EscapeDataString(s.AccountSid)}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["To"] = m.Recipient, ["From"] = s.From, ["Body"] = m.Body }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{s.AccountSid}:{s.AuthToken}")));
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"El proveedor de SMS respondió {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync(ct)}");
    }
}

public static class EmailHtml
{
    public static string Render(OutgoingMessage m)
    {
        var body = WebUtility.HtmlEncode(m.Body).Replace("\n", "<br>");
        // Make links clickable without trusting any HTML from the message itself.
        body = System.Text.RegularExpressions.Regex.Replace(body, @"https?://[^\s<]+", x => $"<a href=\"{x.Value}\" style=\"color:{m.Color}\">{x.Value}</a>");
        return $"""
            <!doctype html><html><body style="margin:0;background:#f4f5f7;font-family:Arial,Helvetica,sans-serif;color:#111827">
            <table width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:24px 12px">
            <table width="560" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:12px;overflow:hidden">
            <tr><td style="background:{m.Color};color:#ffffff;padding:18px 24px;font-size:18px;font-weight:bold">{WebUtility.HtmlEncode(m.ShopName)}</td></tr>
            <tr><td style="padding:24px;font-size:15px;line-height:1.55">{body}</td></tr>
            <tr><td style="padding:14px 24px;font-size:12px;color:#6b7280;border-top:1px solid #e5e7eb">Mensaje automático de {WebUtility.HtmlEncode(m.ShopName)}.</td></tr>
            </table></td></tr></table></body></html>
            """;
    }
}

// Builds customer-facing messages and queues them in the transactional outbox.
public sealed class Notifier(RepairShopDbContext db, IOptions<SaasOptions> saas, SubscriptionService subscriptions)
{
    public string AppUrl => saas.Value.PublicAppUrl.TrimEnd('/');

    public async Task<OrderTracking> EnsureTracking(Guid shop, Guid orderId)
    {
        var t = db.OrderTrackings.Local.FirstOrDefault(x => x.ShopId == shop && x.OrderId == orderId)
            ?? await db.OrderTrackings.SingleOrDefaultAsync(x => x.ShopId == shop && x.OrderId == orderId);
        if (t is not null) return t;
        t = new OrderTracking { ShopId = shop, OrderId = orderId, Code = Saas.ShortCode() };
        db.OrderTrackings.Add(t);
        return t;
    }

    public string TrackUrl(string slug, string code) => $"{AppUrl}/seguimiento/{slug}?codigo={code}";

    public async Task Queue(Guid shop, string recipientEmail, string recipientPhone, string subject, string body, string correlation, string relatedType, Guid? relatedId, bool allowSms = true)
    {
        var profile = await ShopProvisioning.EnsureProfile(db, shop);
        var state = await subscriptions.GetAsync(shop);
        if (!state.Modules.Contains("notifications")) return;
        var now = DateTime.UtcNow;
        if (profile.NotifyEmail && recipientEmail.Contains('@') && !await Exists(shop, correlation + ":email"))
            db.NotificationOutbox.Add(new NotificationOutboxItem(shop, NotificationChannel.Email, recipientEmail.Length > 80 ? recipientEmail[..80] : recipientEmail, Trim(subject, 120), Trim(body, 4000), OutboxStatus.Pending, correlation + ":email", relatedType, relatedId, now));
        var phone = NormalizePhone(recipientPhone);
        if (allowSms && profile.NotifySms && state.Modules.Contains("sms") && phone.Length >= 8 && !await Exists(shop, correlation + ":sms"))
            db.NotificationOutbox.Add(new NotificationOutboxItem(shop, NotificationChannel.Sms, phone, Trim(subject, 120), Trim(body, 600), OutboxStatus.Pending, correlation + ":sms", relatedType, relatedId, now));
    }

    private async Task<bool> Exists(Guid shop, string correlation) =>
        db.NotificationOutbox.Local.Any(x => x.ShopId == shop && x.CorrelationKey == correlation)
        || await db.NotificationOutbox.AnyAsync(x => x.ShopId == shop && x.CorrelationKey == correlation);

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];

    // E.164 for Argentina when the number is written locally (11 1234-5678 -> +5491112345678).
    public static string NormalizePhone(string phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return "";
        if ((phone ?? "").TrimStart().StartsWith('+')) return "+" + digits;
        if (digits.StartsWith("54")) return "+" + digits;
        if (digits.StartsWith('0')) digits = digits[1..];
        return digits.Length is >= 10 and <= 11 ? "+549" + digits : "+" + digits;
    }
}

// Hooks called from the workshop commands. Everything is added to the same DbContext, so it commits with the command.
public sealed class SaasEvents(RepairShopDbContext db, Notifier notifier)
{
    public async Task OrderReceived(Guid shop, Guid orderId, string email)
    {
        var tracking = await notifier.EnsureTracking(shop, orderId);
        if (email.Length > 0)
        {
            tracking.CustomerEmail = Saas.Email(email);
            var contact = await ContactFor(shop, orderId);
            if (contact is not null && contact.Email.Length == 0) contact.Email = tracking.CustomerEmail;
        }
        var (w, profile) = await Context(shop, orderId);
        await notifier.Queue(shop, tracking.CustomerEmail, w.CustomerPhone, $"Recibimos tu {w.DeviceLabel}",
            $"Hola {w.CustomerName}, recibimos tu {w.DeviceLabel} en {profile.DisplayName} (orden {Code(w.Number)}).\nPodés seguir el estado acá: {notifier.TrackUrl(profile.Slug, tracking.Code)}",
            $"order:{orderId}:received", "order", orderId);
        Webhooks.Enqueue(db, shop, "order.created", new { orderId, number = w.Number, w.CustomerName, w.DeviceLabel });
    }

    public async Task StatusChanged(Guid shop, Guid orderId, RepairOrderStatus previous, RepairOrderStatus current)
    {
        Webhooks.Enqueue(db, shop, "order.status_changed", new { orderId, previous = previous.ToString(), status = current.ToString() });
        if (current != RepairOrderStatus.Ready) return;
        var tracking = await notifier.EnsureTracking(shop, orderId);
        var (w, profile) = await Context(shop, orderId);
        await notifier.Queue(shop, await EmailFor(shop, orderId, tracking), w.CustomerPhone, $"Tu {w.DeviceLabel} está listo",
            $"Hola {w.CustomerName}, tu {w.DeviceLabel} (orden {Code(w.Number)}) ya está listo para retirar en {profile.DisplayName}.\n{(profile.Address.Length > 0 ? $"Dirección: {profile.Address}.\n" : "")}Estado: {notifier.TrackUrl(profile.Slug, tracking.Code)}",
            $"order:{orderId}:ready:{DateTime.UtcNow:yyyyMMdd}", "order", orderId);
        db.ShopAlerts.Add(new ShopAlert { ShopId = shop, Kind = "OrderReady", Message = $"{Code(w.Number)} está lista para retirar. Se avisó al cliente.", OrderId = orderId, Link = $"/orders/{orderId}" });
    }

    public async Task Delivered(Guid shop, Guid orderId)
    {
        Webhooks.Enqueue(db, shop, "order.delivered", new { orderId });
        var profile = await ShopProvisioning.EnsureProfile(db, shop);
        if (!profile.SurveysEnabled) return;
        var state = await db.ShopSubscriptions.Where(x => x.ShopId == shop).Select(x => x.Plan).SingleOrDefaultAsync();
        if (state is null || !Plans.Get(state, new SaasOptions()).Modules.Contains("surveys")) return;
        if (await db.SatisfactionSurveys.AnyAsync(x => x.ShopId == shop && x.OrderId == orderId)) return;
        var token = Saas.Token(24);
        db.SatisfactionSurveys.Add(new SatisfactionSurvey { ShopId = shop, OrderId = orderId, TokenHash = Saas.Sha256(token) });
        var tracking = await notifier.EnsureTracking(shop, orderId);
        var (w, _) = await Context(shop, orderId);
        await notifier.Queue(shop, await EmailFor(shop, orderId, tracking), w.CustomerPhone, $"¿Cómo fue tu experiencia en {profile.DisplayName}?",
            $"Hola {w.CustomerName}, gracias por confiar en {profile.DisplayName}. ¿Nos contás cómo fue la reparación de tu {w.DeviceLabel}? Son 10 segundos: {notifier.AppUrl}/encuesta/{token}",
            $"order:{orderId}:survey", "order", orderId);
    }

    public async Task PaymentRegistered(Guid shop, Guid orderId, Guid paymentId, decimal amount, string currency, PaymentMethod method, Guid actor)
    {
        Webhooks.Enqueue(db, shop, "payment.created", new { orderId, paymentId, amount, currency, method = method.ToString() });
        // Order payments land in the open cash session so the daily close includes them.
        var session = await db.CashSessions.FromSqlInterpolated($"SELECT * FROM saas_cash_session WHERE \"ShopId\"={shop} AND \"Status\"='Open' ORDER BY \"CreatedAtUtc\" LIMIT 1 FOR UPDATE").FirstOrDefaultAsync();
        if (session is null) return;
        var w = await db.Workflows.Where(x => x.Id == orderId).Select(x => x.Number).SingleAsync();
        db.CashMovements.Add(new CashMovement { ShopId = shop, SessionId = session.Id, Kind = "OrderPayment", Method = Saas.PaymentMethodName(method), Amount = amount, Currency = currency, Description = $"Cobro {Code(w)}", ReferenceId = paymentId, ActorId = actor });
    }

    public async Task RefundRegistered(Guid shop, Guid orderId, Guid paymentId, decimal amount, Guid actor)
    {
        var payment = await db.CashMovements.Where(x => x.ShopId == shop && x.ReferenceId == paymentId && x.Kind == "OrderPayment").FirstOrDefaultAsync();
        var session = await db.CashSessions.FromSqlInterpolated($"SELECT * FROM saas_cash_session WHERE \"ShopId\"={shop} AND \"Status\"='Open' ORDER BY \"CreatedAtUtc\" LIMIT 1 FOR UPDATE").FirstOrDefaultAsync();
        if (session is null || payment is null) return;
        db.CashMovements.Add(new CashMovement { ShopId = shop, SessionId = session.Id, Kind = "Refund", Method = payment.Method, Amount = -amount, Currency = payment.Currency, Description = "Devolución de cobro de orden", ReferenceId = paymentId, ActorId = actor });
    }

    public async Task QuoteDecided(Guid shop, Guid orderId, Guid quoteId, bool accepted, string name, string signature, string ip, string userAgent)
    {
        if (signature.Length > 0)
        {
            if (!signature.StartsWith("data:image/png;base64,") || signature.Length > 380_000) throw new RepairShop.Domain.Common.DomainException("La firma no es válida.");
            db.PortalSignatures.Add(new PortalSignature { ShopId = shop, OrderId = orderId, QuoteId = quoteId, SignerName = name, Accepted = accepted, SignatureDataUrl = signature, IpAddress = ip, UserAgent = userAgent.Length > 300 ? userAgent[..300] : userAgent });
        }
        var number = await db.Workflows.Where(x => x.Id == orderId).Select(x => x.Number).SingleAsync();
        db.ShopAlerts.Add(new ShopAlert { ShopId = shop, Kind = "QuoteDecision", Message = $"{name} {(accepted ? "aprobó" : "rechazó")} el presupuesto de {Code(number)}.", OrderId = orderId, Link = $"/orders/{orderId}" });
        Webhooks.Enqueue(db, shop, "quote.decided", new { orderId, quoteId, accepted, name });
    }

    private async Task<CustomerContact?> ContactFor(Guid shop, Guid orderId)
    {
        var customerId = await db.RepairOrders.Where(x => x.Id == orderId && x.ShopId == shop).Select(x => x.CustomerId).SingleAsync();
        var contact = db.CustomerContacts.Local.FirstOrDefault(x => x.ShopId == shop && x.CustomerId == customerId)
            ?? await db.CustomerContacts.SingleOrDefaultAsync(x => x.ShopId == shop && x.CustomerId == customerId);
        if (contact is null)
        {
            contact = new CustomerContact { ShopId = shop, CustomerId = customerId };
            db.CustomerContacts.Add(contact);
        }
        return contact;
    }

    private async Task<string> EmailFor(Guid shop, Guid orderId, OrderTracking tracking)
    {
        if (tracking.CustomerEmail.Length > 0) return tracking.CustomerEmail;
        var customerId = await db.RepairOrders.Where(x => x.Id == orderId && x.ShopId == shop).Select(x => x.CustomerId).SingleAsync();
        return await db.CustomerContacts.Where(x => x.ShopId == shop && x.CustomerId == customerId).Select(x => x.Email).SingleOrDefaultAsync() ?? "";
    }

    private async Task<(WorkshopWorkflow, ShopProfile)> Context(Guid shop, Guid orderId)
    {
        var w = db.Workflows.Local.FirstOrDefault(x => x.Id == orderId) ?? await db.Workflows.SingleAsync(x => x.Id == orderId && x.ShopId == shop);
        return (w, await ShopProvisioning.EnsureProfile(db, shop));
    }

    public static string Code(long number) => $"OT-{number:0000}";
}

// Sends queued email/SMS with retries. Rows are claimed with SKIP LOCKED so several API instances can run it.
public sealed class OutboxDispatcher(IServiceScopeFactory scopes, ILogger<OutboxDispatcher> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Tick(stoppingToken); }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { log.LogWarning(ex, "Outbox dispatch failed"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task Tick(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairShopDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var batch = await db.NotificationOutbox.FromSqlInterpolated($@"SELECT * FROM notification_outbox
            WHERE (""Status"" = 0 OR (""Status"" = 3 AND ""AttemptCount"" < 5 AND ""NextAttemptAtUtc"" <= {now}))
            ORDER BY ""CreatedAtUtc"" LIMIT 20 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        foreach (var item in batch)
        {
            if (item.Channel == NotificationChannel.WhatsApp) { item.MarkCancelled(now); continue; }
            var profile = await db.ShopProfiles.SingleOrDefaultAsync(x => x.ShopId == item.ShopId, ct);
            try
            {
                await sender.SendAsync(new OutgoingMessage(item.Channel, item.Recipient, item.Title, item.Body, profile?.DisplayName ?? "", profile?.Email ?? "", profile?.PrimaryColor ?? "#2563eb"), ct);
                item.MarkSent(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                var attempt = item.AttemptCount + 1;
                item.MarkFailed(ex.Message, DateTime.UtcNow.AddMinutes(Math.Pow(3, attempt)), DateTime.UtcNow);
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

// Hourly jobs: pickup reminders, appointment reminders, trial ending notices and the weekly owner summary.
public sealed class SaasScheduler(IServiceScopeFactory scopes, ILogger<SaasScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnce(scopes, stoppingToken); }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { log.LogWarning(ex, "Scheduled SaaS jobs failed"); }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    public static async Task RunOnce(IServiceScopeFactory scopes, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairShopDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<Notifier>();
        var now = DateTime.UtcNow;
        var lockName = "saas:scheduler";
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Only one instance runs the jobs at a time.
        var acquired = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({lockName}, 0)) AS \"Value\"").SingleAsync(ct);
        if (!acquired) return;

        // 1. Devices ready and not picked up.
        var ready = await (from o in db.RepairOrders join w in db.Workflows on o.Id equals w.Id
                           where o.Status == RepairOrderStatus.Ready && w.HandedOverAtUtc == null && !w.IsDemo
                           select new { o.ShopId, o.Id, o.UpdatedAtUtc, w.Number, w.CustomerName, w.CustomerPhone, w.DeviceLabel }).ToListAsync(ct);
        foreach (var o in ready)
        {
            var profile = await ShopProvisioning.EnsureProfile(db, o.ShopId);
            if (profile.PickupReminderDays <= 0 || o.UpdatedAtUtc > now.AddDays(-profile.PickupReminderDays)) continue;
            var tracking = await notifier.EnsureTracking(o.ShopId, o.Id);
            if (tracking.LastPickupReminderAtUtc > now.AddDays(-profile.PickupReminderDays)) continue;
            tracking.LastPickupReminderAtUtc = now;
            var email = tracking.CustomerEmail;
            await notifier.Queue(o.ShopId, email, o.CustomerPhone, $"Tu {o.DeviceLabel} te espera",
                $"Hola {o.CustomerName}, te recordamos que tu {o.DeviceLabel} (orden {SaasEvents.Code(o.Number)}) está listo para retirar en {profile.DisplayName}.",
                $"order:{o.Id}:pickup:{now:yyyyMMdd}", "order", o.Id);
        }

        // 2. Appointments in the next 24 hours.
        var soon = await db.Appointments.Where(x => x.Status == "Booked" || x.Status == "Confirmed").Where(x => x.ReminderSentAtUtc == null && x.StartsAtUtc > now && x.StartsAtUtc < now.AddHours(24)).ToListAsync(ct);
        foreach (var a in soon)
        {
            var profile = await ShopProvisioning.EnsureProfile(db, a.ShopId);
            a.ReminderSentAtUtc = now;
            await notifier.Queue(a.ShopId, a.Email, a.Phone, $"Recordatorio de turno en {profile.DisplayName}",
                $"Hola {a.CustomerName}, te esperamos el {Saas.When(Local(a.StartsAtUtc))} en {profile.DisplayName}{(a.Kind == "Field" ? $" (visita en {a.Address})" : profile.Address.Length > 0 ? $", {profile.Address}" : "")}. Motivo: {a.Reason}.",
                $"appointment:{a.Id}:reminder", "appointment", a.Id);
        }

        // 3. Trial ending in three days: one alert per shop.
        var ending = await db.ShopSubscriptions.Where(x => x.Status == "Trialing" && x.TrialEndsAtUtc > now && x.TrialEndsAtUtc < now.AddDays(3)).ToListAsync(ct);
        foreach (var s in ending)
        {
            var key = $"Tu prueba gratis termina el {Local(s.TrialEndsAtUtc):d/M}.";
            if (!await db.ShopAlerts.AnyAsync(x => x.ShopId == s.ShopId && x.Kind == "Trial" && x.Message.StartsWith(key), ct))
                db.ShopAlerts.Add(new ShopAlert { ShopId = s.ShopId, Kind = "Trial", Message = key + " Elegí un plan para no perder acceso a la carga de datos.", Link = "/settings/plan" });
        }

        // 4. Weekly summary on Monday morning (Argentina).
        var local = Local(now);
        if (local.DayOfWeek == DayOfWeek.Monday && local.Hour == 8)
        {
            var profiles = await db.ShopProfiles.Where(x => x.WeeklySummaryEmail != "").ToListAsync(ct);
            foreach (var p in profiles)
            {
                var since = now.AddDays(-7);
                var created = await db.RepairOrders.CountAsync(x => x.ShopId == p.ShopId && x.CreatedAtUtc >= since, ct);
                var delivered = await db.Workflows.CountAsync(x => x.ShopId == p.ShopId && x.HandedOverAtUtc >= since, ct);
                var collected = await db.RepairOrderPayments.Where(x => x.ShopId == p.ShopId && x.CreatedAtUtc >= since && x.Currency == "ARS").SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
                var sales = await db.CounterSales.Where(x => x.ShopId == p.ShopId && x.CreatedAtUtc >= since && x.Status == "Completed" && x.Currency == "ARS").SumAsync(x => (decimal?)x.Total, ct) ?? 0;
                var scores = await db.SatisfactionSurveys.Where(x => x.ShopId == p.ShopId && x.AnsweredAtUtc >= since && x.Score != null).Select(x => x.Score!.Value).ToListAsync(ct);
                var nps = scores.Count == 0 ? "sin respuestas" : $"{Nps(scores)} ({scores.Count} respuestas)";
                db.NotificationOutbox.Add(new NotificationOutboxItem(p.ShopId, NotificationChannel.Email, p.WeeklySummaryEmail, "Resumen semanal de tu taller",
                    $"Resumen de los últimos 7 días en {p.DisplayName}:\n• Órdenes nuevas: {created}\n• Equipos entregados: {delivered}\n• Cobrado en órdenes: ARS {collected:N2}\n• Ventas de mostrador: ARS {sales:N2}\n• NPS: {nps}\n\nEntrá al panel: {notifier.AppUrl}",
                    OutboxStatus.Pending, $"weekly:{p.ShopId}:{local:yyyyMMdd}", "shop", p.ShopId, now));
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public static int Nps(IReadOnlyCollection<int> scores) =>
        scores.Count == 0 ? 0 : (int)Math.Round((scores.Count(s => s >= 9) - scores.Count(s => s <= 6)) * 100.0 / scores.Count);

    private static readonly TimeZoneInfo Argentina = TimeZoneInfo.CreateCustomTimeZone("ART", TimeSpan.FromHours(-3), "ART", "ART");
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Argentina);
}
