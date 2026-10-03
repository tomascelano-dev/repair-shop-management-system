using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Common;

namespace RepairShop.Infrastructure.Integrations;

/// <summary>Mercado Pago REST client (Checkout Pro preferences + payments lookup).</summary>
public sealed class MercadoPagoClient : IMercadoPagoClient
{
    public const string HttpClientName = "mercadopago";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _http;

    public MercadoPagoClient(IHttpClientFactory http) => _http = http;

    public async Task<MpPreference> CreatePreferenceAsync(string accessToken, MpPreferenceRequest r, CancellationToken ct)
    {
        var client = _http.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, "checkout/preferences");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Add("X-Idempotency-Key", r.ExternalReference);

        var backUrlIsHttps = r.BackUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        req.Content = JsonContent.Create(new
        {
            items = new[] { new { title = r.Title, quantity = 1, unit_price = r.Amount, currency_id = r.Currency } },
            external_reference = r.ExternalReference,
            notification_url = r.NotificationUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? r.NotificationUrl : null,
            back_urls = new { success = r.BackUrl, pending = r.BackUrl, failure = r.BackUrl },
            auto_return = backUrlIsHttps ? "approved" : null,
            expires = true,
            expiration_date_to = r.ExpiresAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'+00:00'", CultureInfo.InvariantCulture),
            payer = string.IsNullOrWhiteSpace(r.PayerEmail) ? null : new { email = r.PayerEmail },
            statement_descriptor = "REPARACION"
        }, options: Json);

        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new DomainException($"Mercado Pago rechazó la creación del link ({(int)res.StatusCode}). Revisá el access token.");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return new MpPreference(root.GetProperty("id").GetString()!, root.GetProperty("init_point").GetString()!);
    }

    public async Task<MpPayment?> GetPaymentAsync(string accessToken, string paymentId, CancellationToken ct)
    {
        if (!paymentId.All(char.IsLetterOrDigit)) return null;

        var client = _http.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"v1/payments/{paymentId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await client.SendAsync(req, ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var p = doc.RootElement;
        DateTime? approved = p.TryGetProperty("date_approved", out var da) && da.ValueKind == JsonValueKind.String
            ? DateTimeOffset.Parse(da.GetString()!, CultureInfo.InvariantCulture).UtcDateTime
            : null;

        return new MpPayment(
            p.GetProperty("id").ToString(),
            p.GetProperty("status").GetString() ?? "",
            p.TryGetProperty("external_reference", out var er) && er.ValueKind == JsonValueKind.String ? er.GetString() : null,
            p.GetProperty("transaction_amount").GetDecimal(),
            p.TryGetProperty("currency_id", out var cur) ? cur.GetString() ?? "ARS" : "ARS",
            approved);
    }
}
