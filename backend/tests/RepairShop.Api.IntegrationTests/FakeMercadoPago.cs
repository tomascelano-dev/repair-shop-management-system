using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RepairShop.Application.Payments;

namespace RepairShop.Api.IntegrationTests;

/// <summary>
/// In-memory stand-in for the Mercado Pago subscriptions API (preapproval) of one platform account. Requests with
/// another access token get 401, like the real API.
/// </summary>
public sealed class FakeMercadoPago
{
    private readonly string _accessToken;
    private readonly ConcurrentDictionary<string, JsonObject> _preapprovals = new();
    private readonly ConcurrentDictionary<string, string> _authorizedPayments = new();

    public FakeMercadoPago(string accessToken) => _accessToken = accessToken;

    public ConcurrentQueue<string> Requests { get; } = new();

    public JsonObject? Preapproval(string id) => _preapprovals.TryGetValue(id, out var p) ? (JsonObject)p.DeepClone() : null;

    /// <summary>The customer paid (or a renewal was charged): the preapproval becomes authorized.</summary>
    public void Authorize(string id, DateTime nextPaymentUtc) => Update(id, p =>
    {
        p["status"] = "authorized";
        p["payer_id"] = 123456;
        p["next_payment_date"] = nextPaymentUtc.ToString("O");
    });

    /// <summary>A recurring charge of the preapproval (topic subscription_authorized_payment).</summary>
    public string AddAuthorizedPayment(string preapprovalId)
    {
        var id = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString();
        _authorizedPayments[id] = preapprovalId;
        return id;
    }

    /// <summary>Notification exactly as Mercado Pago signs it (x-signature over data.id, x-request-id and ts).</summary>
    public static HttpRequestMessage Notification(string path, string type, string dataId, string secret)
    {
        var requestId = Guid.NewGuid().ToString();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var req = new HttpRequestMessage(HttpMethod.Post, $"{path}?data.id={dataId}&type={type}")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { type, action = "updated", data = new { id = dataId } }), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-request-id", requestId);
        req.Headers.Add("x-signature", $"ts={ts},v1={MercadoPagoSignature.Sign(secret, requestId, dataId, ts)}");
        return req;
    }

    private void Update(string id, Action<JsonObject> change)
    {
        var p = _preapprovals[id];
        lock (p)
        {
            change(p);
            p["last_modified"] = DateTime.UtcNow.ToString("O");
        }
    }

    public sealed class Handler : HttpMessageHandler
    {
        private readonly FakeMercadoPago _mp;
        public Handler(FakeMercadoPago mp) => _mp = mp;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath.Trim('/');
            _mp.Requests.Enqueue($"{request.Method} {path}");
            if (request.Headers.Authorization?.Parameter != _mp._accessToken)
                return Json(HttpStatusCode.Unauthorized, new JsonObject { ["error"] = "unauthorized", ["message"] = "invalid access token" });

            var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct)) as JsonObject;
            var parts = path.Split('/');

            if (request.Method == HttpMethod.Post && path == "preapproval")
            {
                var id = Guid.NewGuid().ToString("N");
                var p = new JsonObject
                {
                    ["id"] = id,
                    ["status"] = "pending",
                    ["reason"] = body!["reason"]?.DeepClone(),
                    ["external_reference"] = body["external_reference"]?.DeepClone(),
                    ["payer_email"] = body["payer_email"]?.DeepClone(),
                    ["auto_recurring"] = body["auto_recurring"]?.DeepClone(),
                    ["back_url"] = body["back_url"]?.DeepClone(),
                    ["init_point"] = $"https://www.mercadopago.com.mx/subscriptions/checkout?preapproval_id={id}",
                    ["last_modified"] = DateTime.UtcNow.ToString("O"),
                };
                _mp._preapprovals[id] = p;
                return Json(HttpStatusCode.Created, p);
            }

            if (parts.Length == 2 && parts[0] == "preapproval" && _mp._preapprovals.TryGetValue(parts[1], out var existing))
            {
                if (request.Method == HttpMethod.Get) return Json(HttpStatusCode.OK, _mp.Preapproval(parts[1])!);
                if (request.Method == HttpMethod.Put)
                {
                    _mp.Update(parts[1], p =>
                    {
                        foreach (var (key, value) in body!)
                        {
                            if (key == "auto_recurring" && value is JsonObject ar && p["auto_recurring"] is JsonObject current)
                                foreach (var (k, v) in ar) current[k] = v?.DeepClone();
                            else p[key] = value?.DeepClone();
                        }
                    });
                    return Json(HttpStatusCode.OK, _mp.Preapproval(parts[1])!);
                }
            }

            if (parts.Length == 2 && parts[0] == "authorized_payments" && request.Method == HttpMethod.Get
                && _mp._authorizedPayments.TryGetValue(parts[1], out var preapprovalId))
                return Json(HttpStatusCode.OK, new JsonObject { ["id"] = long.Parse(parts[1]), ["preapproval_id"] = preapprovalId, ["status"] = "processed" });

            return Json(HttpStatusCode.NotFound, new JsonObject { ["error"] = "not_found", ["message"] = path });
        }

        private static HttpResponseMessage Json(HttpStatusCode status, JsonObject body)
            => new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    }
}
