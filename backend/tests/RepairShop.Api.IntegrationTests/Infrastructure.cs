using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RepairShop.Api.IntegrationTests;

/// <summary>
/// Runs only when REPAIRSHOP_TEST_CONNECTION points to a PostgreSQL server (CI uses a service container),
/// e.g. "Host=localhost;Port=5432;Username=postgres;Password=postgres". Each run uses a throwaway database.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(TestDatabase.ServerConnection))
            Skip = "Definí REPAIRSHOP_TEST_CONNECTION para correr los tests de integración.";
    }
}

public static class TestDatabase
{
    public static string? ServerConnection => Environment.GetEnvironmentVariable("REPAIRSHOP_TEST_CONNECTION");

    public static string Create(string prefix)
    {
        var name = $"{prefix}_{Guid.NewGuid():N}"; // <= 63 chars (PostgreSQL identifier limit)
        using var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ServerConnection) { Database = "postgres" }.ConnectionString);
        conn.Open();
        using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn)) cmd.ExecuteNonQuery();
        return new NpgsqlConnectionStringBuilder(ServerConnection) { Database = name }.ConnectionString;
    }

    public static void Drop(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database;
        NpgsqlConnection.ClearAllPools();
        using var conn = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ServerConnection) { Database = "postgres" }.ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", conn);
        cmd.ExecuteNonQuery();
    }
}

/// <summary>The API running in memory against a fresh database (migrated and seeded with the dev users).</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string PaddleWebhookSecret = "pdl_ntfset_integration_tests";

    public string ConnectionString { get; }

    public ApiFactory()
    {
        ConnectionString = TestDatabase.Create("rs_it");
        var settings = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ConnectionStrings__RepairShopDb"] = ConnectionString,
            ["Jwt__Key"] = "integration-tests-signing-key-0123456789abcdef",
            ["Seed__Enabled"] = "true",
            ["Seed__DemoData"] = "false",
            ["BackgroundJobs__Enabled"] = "false",
            ["Notifications__WhatsApp__Provider"] = "Simulated",
            ["Notifications__Email__Provider"] = "Simulated",
            ["RateLimiting__AuthPerMinute"] = "100000",
            ["RateLimiting__RefreshPerMinute"] = "100000",
            ["RateLimiting__PublicPerMinute"] = "100000",
            ["App__PublicApiUrl"] = "http://localhost",
            ["App__FrontendBaseUrl"] = "http://localhost:5173",
            // Paddle "configured" so its webhooks are accepted (tests never call its API); Mercado Pago is not,
            // so checkouts from Argentina fall back to the simulated provider.
            ["Billing__AllowSimulated"] = "true",
            ["Billing__Paddle__ApiKey"] = "pdl_test_key",
            ["Billing__Paddle__ClientToken"] = "test_client_token",
            ["Billing__Paddle__WebhookSecret"] = PaddleWebhookSecret,
            ["Billing__Paddle__PriceIds__Basic"] = "pri_basic",
            ["Billing__Paddle__PriceIds__Standard"] = "pri_standard",
            ["Billing__Paddle__PriceIds__Pro"] = "pri_pro",
        };
        foreach (var (key, value) in settings) Environment.SetEnvironmentVariable(key, value);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) TestDatabase.Drop(ConnectionString);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static async Task<JsonElement> DataAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new Xunit.Sdk.XunitException($"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }

    public static string Str(this JsonElement e, string name) => e.GetProperty(name).ToString();
    public static decimal Dec(this JsonElement e, string name) => e.GetProperty(name).GetDecimal();
}

/// <summary>HTTP client logged in as a seeded user (refresh cookie handled by the client's cookie container).</summary>
public sealed class ApiClient
{
    public HttpClient Http { get; }

    private ApiClient(HttpClient http) => Http = http;

    public static async Task<ApiClient> LoginAsync(ApiFactory factory, string email = "admin@local", string password = "Admin123456")
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, BaseAddress = new Uri("http://localhost") });
        http.DefaultRequestHeaders.Add("X-RS-Refresh", "1");
        var res = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var data = await res.DataAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.Str("accessToken"));
        return new ApiClient(http);
    }

    /// <summary>Signs up a brand-new shop from the website and returns a client logged in as its owner.</summary>
    public static async Task<(ApiClient Client, JsonElement Login)> SignupAsync(ApiFactory factory, string country = "AR", string? email = null)
    {
        var http = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, BaseAddress = new Uri("http://localhost") });
        http.DefaultRequestHeaders.Add("X-RS-Refresh", "1");
        email ??= $"owner{Guid.NewGuid():N}"[..20] + "@example.com";
        var res = await http.PostAsJsonAsync("/api/v1/auth/signup", new
        {
            shopName = "Taller " + Guid.NewGuid().ToString("N")[..6],
            ownerName = "Dueña Test",
            email,
            password = "Clave12345",
            country,
            timeZone = "America/Mexico_City",
            acceptTerms = true
        });
        var data = await res.DataAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.Str("accessToken"));
        return (new ApiClient(http), data);
    }

    public void UseToken(string token) => Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public Task<HttpResponseMessage> Get(string path) => Http.GetAsync("/api/v1" + path);

    public Task<HttpResponseMessage> Post(string path, object? body = null, string? idempotencyKey = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1" + path) { Content = JsonContent.Create(body ?? new { }, options: Json.Options) };
        if (idempotencyKey is not null) req.Headers.Add("Idempotency-Key", idempotencyKey);
        return Http.SendAsync(req);
    }

    public Task<HttpResponseMessage> Put(string path, object body) => Http.PutAsJsonAsync("/api/v1" + path, body, Json.Options);

    /// <summary>Creates customer + device + order and returns the order.</summary>
    public async Task<JsonElement> CreateOrderAsync(string issue = "No enciende, se mojó")
    {
        var phone = $"11 {Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}";
        var customer = await (await Post("/customers", new { fullName = "Cliente Test", phone, notes = (string?)null })).DataAsync();
        var device = await (await Post("/devices", new { customerId = customer.Str("id"), brand = "Samsung", model = "Galaxy A54", label = (string?)null, serialNumber = (string?)null, notes = (string?)null })).DataAsync();
        return await (await Post("/orders", new { customerId = customer.Str("id"), deviceId = device.Str("id"), issueDescription = issue, notes = (string?)null })).DataAsync();
    }

    public async Task<JsonElement> CreateItemAsync(int qty, decimal salePrice = 1000m, bool sellable = true)
    {
        var sku = "IT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return await (await Post("/inventory", new
        {
            sku, name = "Ítem " + sku, initialQuantity = qty, unitCost = salePrice / 2, unitCostCurrency = "ARS",
            isSellable = sellable, salePrice, salePriceCurrency = "ARS"
        })).DataAsync();
    }

    public async Task EnsureCashOpenAsync()
    {
        var current = await (await Get("/cash/current")).DataAsync();
        if (current.ValueKind == JsonValueKind.Null)
            await (await Post("/cash/open", new { openingCash = 0, currency = "ARS" })).DataAsync();
    }
}
