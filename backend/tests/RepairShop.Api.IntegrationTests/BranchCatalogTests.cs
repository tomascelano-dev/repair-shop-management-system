using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;

namespace RepairShop.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class BranchCatalogTests
{
    private readonly ApiFactory _factory;

    public BranchCatalogTests(ApiFactory factory) => _factory = factory;

    [IntegrationFact]
    public async Task A_new_branch_copies_the_whole_catalog_without_stock()
    {
        var admin = await ApiClient.LoginAsync(_factory);
        var prefix = "CP" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        // More items than one repository page (200) to make sure the copy pages through everything.
        var csv = new StringBuilder("sku,nombre,stock\n");
        for (var i = 0; i < 230; i++) csv.Append($"{prefix}-{i:D3},Repuesto {prefix} {i},4\n");
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv.ToString()));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "catalogo.csv");
        var imported = await (await admin.Http.PostAsync("/api/v1/inventory/import?dryRun=false", form)).DataAsync();
        imported.GetProperty("created").GetInt32().Should().Be(230);

        var branch = await (await admin.Post("/settings/branches", new { name = "Sucursal " + prefix, copyTemplates = false, copyCatalog = true })).DataAsync();
        var switched = await (await admin.Post("/auth/switch-shop", new { shopId = branch.Str("id") })).DataAsync();
        admin.UseToken(switched.Str("accessToken"));

        var page = await admin.Get($"/inventory?q={prefix}&take=1");
        page.Headers.GetValues("X-Total-Count").Single().Should().Be("230");
        var last = await (await admin.Get($"/inventory/by-code/{prefix}-229")).DataAsync();
        last.GetProperty("quantityOnHand").GetInt32().Should().Be(0);
    }
}
