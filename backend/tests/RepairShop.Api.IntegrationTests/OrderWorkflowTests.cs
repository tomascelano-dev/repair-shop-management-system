using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace RepairShop.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class OrderWorkflowTests
{
    private readonly ApiFactory _factory;

    public OrderWorkflowTests(ApiFactory factory) => _factory = factory;

    [IntegrationFact]
    public async Task Full_repair_flow_with_quote_from_portal_reservation_qa_payment_and_delivery()
    {
        var api = await ApiClient.LoginAsync(_factory);
        var order = await api.CreateOrderAsync();
        var id = order.Str("id");
        order.Str("code").Should().MatchRegex(@"^#\d{6}$");

        await (await api.Post($"/orders/{id}/status", new { status = "Diagnosing" })).DataAsync();
        (await api.Post($"/orders/{id}/status", new { status = "InProgress" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var part = await api.CreateItemAsync(qty: 3, salePrice: 8000);
        var quote = await (await api.Post($"/orders/{id}/quotes", new
        {
            currency = "ARS",
            items = new object[]
            {
                new { kind = "Labor", description = "Mano de obra", quantity = 1, unitPrice = 12000 },
                new { kind = "Part", description = "Pin de carga", quantity = 1, unitPrice = 8000, inventoryItemId = part.Str("id") },
            }
        })).DataAsync();
        quote.Dec("total").Should().Be(20000);
        await (await api.Post($"/orders/{id}/quotes/{quote.Str("id")}/send", new { validDays = 5 })).DataAsync();

        // Customer approves from the tracking portal (no login).
        var current = await (await api.Get($"/orders/{id}")).DataAsync();
        var token = current.Str("trackingUrl").Split('/').Last();
        var anon = _factory.CreateClient();
        var portal = await (await anon.PostAsJsonAsync($"/api/v1/public/orders/{token}/quotes/{quote.Str("id")}/approve", new { note = "Dale" })).DataAsync();
        portal.GetProperty("quote").Str("status").Should().Be("Approved");

        var item = await (await api.Get($"/inventory/{part.Str("id")}")).DataAsync();
        item.GetProperty("reservedQuantity").GetInt32().Should().Be(1);
        item.GetProperty("availableQuantity").GetInt32().Should().Be(2);

        await (await api.Post($"/orders/{id}/status", new { status = "InProgress" })).DataAsync();
        var used = await (await api.Post($"/orders/{id}/parts", new { inventoryItemId = part.Str("id"), quantityUsed = 1 })).DataAsync();
        used[0].GetProperty("chargedToCustomer").GetBoolean().Should().BeFalse("the part is already in the approved quote");

        (await api.Post($"/orders/{id}/status", new { status = "Ready" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await (await api.Put($"/orders/{id}/qa", new { powersOn = true, screenOk = true, chargingOk = true, approve = true })).DataAsync();
        await (await api.Post($"/orders/{id}/status", new { status = "Ready" })).DataAsync();

        await api.EnsureCashOpenAsync();
        (await api.Post($"/orders/{id}/status", new { status = "Delivered" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await api.Post($"/orders/{id}/payments", new { amount = 25000, currency = "ARS", method = "Cash" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await (await api.Post($"/orders/{id}/payments", new { amount = 5000, currency = "ARS", method = "Transfer", reference = "seña" })).DataAsync();
        await (await api.Post($"/orders/{id}/payments", new { amount = 15000, currency = "ARS", method = "Cash" })).DataAsync();
        await (await api.Post($"/orders/{id}/status", new { status = "Delivered" })).DataAsync();

        var delivered = await (await api.Get($"/orders/{id}")).DataAsync();
        delivered.Dec("balanceDue").Should().Be(0);
        delivered.GetProperty("underWarranty").GetBoolean().Should().BeTrue();

        var history = await (await api.Get($"/orders/{id}/history")).DataAsync();
        history.GetArrayLength().Should().Be(4); // Diagnosing, InProgress, Ready, Delivered
    }

    [IntegrationFact]
    public async Task Search_finds_orders_by_code_customer_and_device()
    {
        var api = await ApiClient.LoginAsync(_factory);
        var order = await api.CreateOrderAsync("Pantalla con líneas verdes");

        foreach (var q in new[] { order.Str("code"), order.Str("orderNumber"), "Cliente Test", "Galaxy A54", "líneas verdes" })
        {
            var found = await (await api.Get($"/orders?q={Uri.EscapeDataString(q)}&take=200")).DataAsync();
            found.EnumerateArray().Should().Contain(o => o.Str("id") == order.Str("id"), $"searching '{q}'");
        }
    }

    [IntegrationFact]
    public async Task Board_groups_open_orders_by_status()
    {
        var api = await ApiClient.LoginAsync(_factory);
        var order = await api.CreateOrderAsync();
        var board = await (await api.Get("/orders/board")).DataAsync();
        var received = board.GetProperty("columns").EnumerateArray().Single(c => c.Str("status") == "Received");
        received.GetProperty("items").EnumerateArray().Should().Contain(o => o.Str("id") == order.Str("id"));
    }

    [IntegrationFact]
    public async Task Idempotency_key_prevents_duplicate_orders()
    {
        var api = await ApiClient.LoginAsync(_factory);
        var first = await api.CreateOrderAsync();
        var key = Guid.NewGuid().ToString();
        var body = new { customerId = first.Str("customerId"), deviceId = first.Str("deviceId"), issueDescription = "Reingreso por la misma falla" };

        var a = await (await api.Post("/orders", body, key)).DataAsync();
        var replay = await api.Post("/orders", body, key);
        (await replay.DataAsync()).Str("id").Should().Be(a.Str("id"));
        replay.Headers.GetValues("X-Idempotency-Replay").Should().Contain("true");

        var other = await api.Post("/orders", new { body.customerId, body.deviceId, issueDescription = "Otra falla distinta" }, key);
        other.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [IntegrationFact]
    public async Task Validation_errors_are_in_spanish_with_camel_case_fields()
    {
        var api = await ApiClient.LoginAsync(_factory);
        var res = await api.Post("/customers", new { fullName = "A", phone = "1" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"fullName\"").And.Contain("caracteres");
    }
}
