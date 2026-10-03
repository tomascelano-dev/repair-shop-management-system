using System.Net;
using FluentAssertions;

namespace RepairShop.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class TenantIsolationTests
{
    private readonly ApiFactory _factory;

    public TenantIsolationTests(ApiFactory factory) => _factory = factory;

    [IntegrationFact]
    public async Task Data_of_one_branch_is_invisible_from_another()
    {
        var admin = await ApiClient.LoginAsync(_factory);
        var order = await admin.CreateOrderAsync("Equipo de la casa central");
        var customerId = order.Str("customerId");

        var branch = await (await admin.Post("/settings/branches", new { name = "Sucursal " + Guid.NewGuid().ToString("N")[..5], copyTemplates = true })).DataAsync();
        var switched = await (await admin.Post("/auth/switch-shop", new { shopId = branch.Str("id") })).DataAsync();
        admin.UseToken(switched.Str("accessToken"));

        (await admin.Get($"/orders/{order.Str("id")}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Get($"/customers/{customerId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var list = await (await admin.Get("/orders")).DataAsync();
        list.EnumerateArray().Should().NotContain(o => o.Str("id") == order.Str("id"));

        // Cross-branch references are rejected too (order in this branch for a customer of the other one).
        var res = await admin.Post("/orders", new { customerId, deviceId = order.Str("deviceId"), issueDescription = "Intento cruzado" });
        res.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }
}
