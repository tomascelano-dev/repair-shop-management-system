using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using RepairShop.Domain.Notifications;

namespace RepairShop.Infrastructure.Notifications;

public sealed record SendResult(bool Success, string Provider, string? ProviderMessageId = null, string? Error = null, bool Permanent = false);

public interface INotificationSender
{
    string Name { get; }
    Task<SendResult> SendAsync(NotificationOutboxItem item, CancellationToken ct);
}

/// <summary>Development/demo sender: logs the message and marks it as sent.</summary>
public sealed class SimulatedNotificationSender : INotificationSender
{
    private readonly ILogger<SimulatedNotificationSender> _logger;
    public SimulatedNotificationSender(ILogger<SimulatedNotificationSender> logger) => _logger = logger;

    public string Name => "simulated";

    public Task<SendResult> SendAsync(NotificationOutboxItem item, CancellationToken ct)
    {
        _logger.LogInformation("[SIMULATED {Channel}] To {Recipient}: {Title}\n{Body}", item.Channel, item.Recipient, item.Title, item.Body);
        return Task.FromResult(new SendResult(true, Name, $"sim-{item.Id:N}"));
    }
}

/// <summary>Twilio Messages API (WhatsApp and SMS).</summary>
public sealed class TwilioNotificationSender : INotificationSender
{
    public const string HttpClientName = "twilio";

    private readonly IHttpClientFactory _http;
    private readonly NotificationOptions.TwilioOptions _opt;

    public TwilioNotificationSender(IHttpClientFactory http, IOptions<NotificationOptions> options)
    {
        _http = http;
        _opt = options.Value.Twilio;
    }

    public string Name => "twilio";

    public async Task<SendResult> SendAsync(NotificationOutboxItem item, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccountSid) || string.IsNullOrWhiteSpace(_opt.AuthToken))
            return new SendResult(false, Name, Error: "Twilio no está configurado.", Permanent: true);

        var whatsapp = item.Channel == NotificationChannel.WhatsApp;
        var from = whatsapp ? _opt.WhatsAppFrom : _opt.SmsFrom;
        if (string.IsNullOrWhiteSpace(from)) return new SendResult(false, Name, Error: "Falta el remitente de Twilio.", Permanent: true);

        var to = "+" + item.Recipient.TrimStart('+');
        var client = _http.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"2010-04-01/Accounts/{_opt.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = whatsapp ? $"whatsapp:{to}" : to,
                ["From"] = from,
                ["Body"] = item.Body
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_opt.AccountSid}:{_opt.AuthToken}")));

        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(body);
            return new SendResult(true, Name, doc.RootElement.TryGetProperty("sid", out var sid) ? sid.GetString() : null);
        }

        // 4xx (bad number, unverified sender...) won't succeed on retry.
        var permanent = (int)res.StatusCode is >= 400 and < 500 && res.StatusCode != System.Net.HttpStatusCode.TooManyRequests;
        return new SendResult(false, Name, Error: $"Twilio {(int)res.StatusCode}: {Truncate(body)}", Permanent: permanent);
    }

    private static string Truncate(string s) => s.Length > 500 ? s[..500] : s;
}

/// <summary>WhatsApp Business Cloud API (Meta).</summary>
public sealed class MetaWhatsAppSender : INotificationSender
{
    public const string HttpClientName = "meta";

    private readonly IHttpClientFactory _http;
    private readonly NotificationOptions.MetaOptions _opt;

    public MetaWhatsAppSender(IHttpClientFactory http, IOptions<NotificationOptions> options)
    {
        _http = http;
        _opt = options.Value.Meta;
    }

    public string Name => "meta";

    public async Task<SendResult> SendAsync(NotificationOutboxItem item, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.PhoneNumberId) || string.IsNullOrWhiteSpace(_opt.AccessToken))
            return new SendResult(false, Name, Error: "WhatsApp Cloud API no está configurada.", Permanent: true);

        object payload = string.IsNullOrWhiteSpace(_opt.TemplateName)
            ? new { messaging_product = "whatsapp", to = item.Recipient, type = "text", text = new { preview_url = true, body = item.Body } }
            : new
            {
                messaging_product = "whatsapp",
                to = item.Recipient,
                type = "template",
                template = new
                {
                    name = _opt.TemplateName,
                    language = new { code = _opt.TemplateLanguage },
                    // Template variables can't contain new lines.
                    components = new[] { new { type = "body", parameters = new[] { new { type = "text", text = item.Body.Replace("\r", "").Replace("\n", " · ") } } } }
                }
            };

        var client = _http.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_opt.ApiVersion}/{_opt.PhoneNumberId}/messages") { Content = JsonContent.Create(payload) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.AccessToken);

        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0 ? msgs[0].GetProperty("id").GetString() : null;
            return new SendResult(true, Name, id);
        }

        var permanent = (int)res.StatusCode is 400 or 401 or 403 or 404;
        return new SendResult(false, Name, Error: $"Meta {(int)res.StatusCode}: {(body.Length > 500 ? body[..500] : body)}", Permanent: permanent);
    }
}

/// <summary>SMTP email (works with Gmail, Resend, SendGrid, SES... SMTP relays).</summary>
public sealed class SmtpEmailSender : INotificationSender
{
    private readonly NotificationOptions.SmtpOptions _opt;

    public SmtpEmailSender(IOptions<NotificationOptions> options) => _opt = options.Value.Smtp;

    public string Name => "smtp";

    public async Task<SendResult> SendAsync(NotificationOutboxItem item, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.Host) || string.IsNullOrWhiteSpace(_opt.FromAddress))
            return new SendResult(false, Name, Error: "SMTP no está configurado.", Permanent: true);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opt.FromName, _opt.FromAddress));
        message.To.Add(MailboxAddress.Parse(item.Recipient));
        message.Subject = item.Title;
        message.Body = new TextPart("plain") { Text = item.Body };

        var security = _opt.Security.ToLowerInvariant() switch
        {
            "starttls" => SecureSocketOptions.StartTls,
            "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "none" => SecureSocketOptions.None,
            _ => SecureSocketOptions.Auto
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(_opt.Host, _opt.Port, security, ct);
        if (!string.IsNullOrWhiteSpace(_opt.Username)) await client.AuthenticateAsync(_opt.Username, _opt.Password, ct);
        var response = await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
        return new SendResult(true, Name, response);
    }
}
