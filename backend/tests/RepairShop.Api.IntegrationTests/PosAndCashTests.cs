using System.Net;
using FluentAssertions;

namespace RepairShop.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PosAndCashTests
{
    private readonly ApiFactory _factory;

    public PosAndCashTests(ApiFactory factory) => _factory = factory;

    [IntegrationFact]
    public async Task Sale_moves_stock_and_cash_and_is_idempotent()
    {
        var api = await ApiClient.LoginAsync(_factory);
        await api.EnsureCashOpenAsync();
        var item = await api.CreateItemAsync(qty: 10, salePrice: 2500);
        var body = new
        {
            lines = new[] { new { inventoryItemId = item.Str("id"), quantity = 3 } },
            payments = new object[] { new { method = "Card", amount = 5000 }, new { method = "Cash", amount = 3000 } },
            discountAmount = 0
        };
        var key = Guid.NewGuid().ToString();

        var sale = await (await api.Post("/sales", body, key)).DataAsync();
        sale.Dec("total").Should().Be(7500);
        sale.Dec("changeAmount").Should().Be(500);
        (await (await api.Post("/sales", body, key)).DataAsync()).Str("id").Should().Be(sale.Str("id"));

        (await (await api.Get($"/inventory/{item.Str("id")}")).DataAsync()).GetProperty("quantityOnHand").GetInt32().Should().Be(7);

        var cash = await (await api.Get("/cash/current")).DataAsync();
        var cashLine = cash.GetProperty("summary").EnumerateArray().Single(l => l.Str("method") == "Cash" && l.Str("currency") == "ARS");
        cashLine.Dec("inflows").Should().BeGreaterOrEqualTo(2500);
    }

    [IntegrationFact]
    public async Task Concurrent_sales_of_the_last_unit_never_oversell()
    {
        var api = await ApiClient.LoginAsync(_factory);
        await api.EnsureCashOpenAsync();
        var item = await api.CreateItemAsync(qty: 1, salePrice: 1000);
        var body = new
        {
            lines = new[] { new { inventoryItemId = item.Str("id"), quantity = 1 } },
            payments = new[] { new { method = "Cash", amount = 1000 } }
        };

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => api.Post("/sales", body)));

        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        results.Where(r => r.StatusCode != HttpStatusCode.Created).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.BadRequest || r.StatusCode == HttpStatusCode.Conflict);
        (await (await api.Get($"/inventory/{item.Str("id")}")).DataAsync()).GetProperty("quantityOnHand").GetInt32().Should().Be(0);
    }

    [IntegrationFact]
    public async Task Refund_restocks_and_closing_the_register_reports_differences()
    {
        var api = await ApiClient.LoginAsync(_factory);
        await api.EnsureCashOpenAsync();
        var item = await api.CreateItemAsync(qty: 5, salePrice: 1000);
        var sale = await (await api.Post("/sales", new
        {
            lines = new[] { new { inventoryItemId = item.Str("id"), quantity = 2 } },
            payments = new[] { new { method = "Cash", amount = 2000 } }
        })).DataAsync();

        var lineId = sale.GetProperty("lines")[0].Str("id");
        var refunded = await (await api.Post($"/sales/{sale.Str("id")}/refund", new { lines = new[] { new { saleLineId = lineId, quantity = 1 } }, method = "Cash", restock = true, reason = "Falla" })).DataAsync();
        refunded.Str("status").Should().Be("PartiallyRefunded");
        (await (await api.Get($"/inventory/{item.Str("id")}")).DataAsync()).GetProperty("quantityOnHand").GetInt32().Should().Be(4);

        var closed = await (await api.Post("/cash/close", new { countedCash = 0 })).DataAsync();
        closed.Str("status").Should().Be("Closed");
        closed.Dec("difference").Should().BeLessThan(0);
        (await (await api.Get("/cash/current")).DataAsync()).ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [IntegrationFact]
    public async Task Cash_payments_require_an_open_register()
    {
        var api = await ApiClient.LoginAsync(_factory);
        var current = await (await api.Get("/cash/current")).DataAsync();
        if (current.ValueKind != System.Text.Json.JsonValueKind.Null) await (await api.Post("/cash/close", new { countedCash = 0 })).DataAsync();

        var item = await api.CreateItemAsync(qty: 2, salePrice: 1000);
        var res = await api.Post("/sales", new { lines = new[] { new { inventoryItemId = item.Str("id"), quantity = 1 } }, payments = new[] { new { method = "Cash", amount = 1000 } } });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("caja");
    }
}
