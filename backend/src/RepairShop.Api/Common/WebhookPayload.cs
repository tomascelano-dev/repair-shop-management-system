using System.Text.Json;

namespace RepairShop.Api.Common;

public static class WebhookPayload
{
    /// <summary>Mercado Pago sends the topic and id in the query string (legacy) or in the JSON body.</summary>
    public static async Task<(string? Type, string? DataId)> ReadMercadoPagoAsync(HttpRequest request, CancellationToken ct)
    {
        string? type = request.Query["type"].FirstOrDefault() ?? request.Query["topic"].FirstOrDefault();
        string? dataId = request.Query["data.id"].FirstOrDefault() ?? request.Query["id"].FirstOrDefault();

        if (request.ContentLength is > 0 and < 64 * 1024)
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
                var root = doc.RootElement;
                if (type is null && root.TryGetProperty("type", out var t)) type = t.GetString();
                if (dataId is null && root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
                    dataId = id.ValueKind == JsonValueKind.Number ? id.GetRawText() : id.GetString();
            }
            catch (JsonException)
            {
                throw new BadHttpRequestException("El cuerpo de la notificación no es JSON válido.");
            }
        }

        return (type, dataId);
    }
}
