using System.Globalization;
using System.Text.Json;
using RepairShop.Application.Abstractions;

namespace RepairShop.Infrastructure.Integrations;

/// <summary>USD/ARS rates from https://dolarapi.com (oficial and blue, "venta" price).</summary>
public sealed class DolarApiExchangeRateProvider : IExchangeRateProvider
{
    public const string HttpClientName = "dolarapi";

    private readonly IHttpClientFactory _http;
    public DolarApiExchangeRateProvider(IHttpClientFactory http) => _http = http;

    public async Task<IReadOnlyList<FetchedRate>> FetchAsync(CancellationToken ct)
    {
        var client = _http.CreateClient(HttpClientName);
        using var res = await client.GetAsync("v1/dolares", ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var result = new List<FetchedRate>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var casa = e.TryGetProperty("casa", out var c) ? c.GetString() : null;
            if (casa is not ("oficial" or "blue")) continue;
            if (!e.TryGetProperty("venta", out var venta) || venta.ValueKind != JsonValueKind.Number) continue;

            var date = e.TryGetProperty("fechaActualizacion", out var f) && f.ValueKind == JsonValueKind.String
                ? DateOnly.FromDateTime(DateTimeOffset.Parse(f.GetString()!, CultureInfo.InvariantCulture).UtcDateTime)
                : DateOnly.FromDateTime(DateTime.UtcNow);
            result.Add(new FetchedRate(casa, "USD", "ARS", venta.GetDecimal(), date));
        }

        return result;
    }
}

public sealed class NoExchangeRateProvider : IExchangeRateProvider
{
    public Task<IReadOnlyList<FetchedRate>> FetchAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FetchedRate>>(Array.Empty<FetchedRate>());
}
