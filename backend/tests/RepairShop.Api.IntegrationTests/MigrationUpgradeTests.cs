using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.IntegrationTests;

/// <summary>Upgrading a database created by the first version (InitialCreate) keeps and repairs legacy data.</summary>
public sealed class MigrationUpgradeTests
{
    [IntegrationFact]
    public async Task Legacy_database_is_upgraded_without_losing_data()
    {
        var cs = TestDatabase.Create("rs_upg");
        try
        {
            await using (var db = NewContext(cs))
                await db.GetService<IMigrator>().MigrateAsync("20260129000000_InitialCreate");

            await ExecAsync(cs, """
                INSERT INTO shops VALUES ('11111111-1111-1111-1111-111111111111', 'Taller', NULL, NULL, NULL, 'AR', true, '2025-01-01', '2025-01-01');
                INSERT INTO users VALUES ('aaaaaaaa-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000000', 'viejo@local', 'Viejo', 2, 'PBKDF2$hash-de-prueba-suficientemente-largo', '2025-01-01');
                INSERT INTO customers VALUES ('cccccccc-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'Juan Pérez', '011 15 2345-6789', NULL, '2025-02-01');
                INSERT INTO devices VALUES ('dddddddd-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'cccccccc-0000-0000-0000-000000000001', 'Apple', 'iPhone 11', NULL, NULL, NULL, '2025-02-01');
                INSERT INTO repair_orders VALUES ('eeeeeeee-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'cccccccc-0000-0000-0000-000000000001', 'dddddddd-0000-0000-0000-000000000001', 'Pantalla rota', NULL, 4, 1000, 'ARS', NULL, NULL, '2025-02-02', '2025-02-05');
                INSERT INTO repair_orders VALUES ('eeeeeeee-0000-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', 'cccccccc-0000-0000-0000-0000000000ff', 'dddddddd-0000-0000-0000-0000000000ff', 'Huérfana', NULL, 0, NULL, NULL, NULL, NULL, '2025-02-03', '2025-02-03');
                INSERT INTO order_payments VALUES (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', 'eeeeeeee-0000-0000-0000-0000000000aa', 500, 'ARS', 0, NULL, 'aaaaaaaa-0000-0000-0000-000000000001', '2025-02-04');
                INSERT INTO order_notes VALUES (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', 'eeeeeeee-0000-0000-0000-0000000000bb', 'nota sin orden', 'aaaaaaaa-0000-0000-0000-000000000001', '2025-02-04');
                INSERT INTO notification_outbox VALUES (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', 0, '5491123456789', NULL, 'viejo', 0, 0, NULL, NULL, 'k', NULL, NULL, '2025-02-04', '2025-02-04');
                """);

            await using (var db = NewContext(cs))
                await db.Database.MigrateAsync();

            (await ScalarAsync<long>(cs, """SELECT count(*) FROM repair_orders""")).Should().Be(3, "the orphan payment is kept under a recovered order");
            (await ScalarAsync<string>(cs, """SELECT string_agg("OrderNumber"::text, ',' ORDER BY "OrderNumber") FROM repair_orders""")).Should().Be("1,2,3");
            (await ScalarAsync<int>(cs, """SELECT "Value" FROM shop_counters WHERE "Key" = 'repair_order'""")).Should().Be(3);
            (await ScalarAsync<long>(cs, """SELECT count(DISTINCT "PublicToken") FROM repair_orders WHERE length("PublicToken") = 64""")).Should().Be(3);
            (await ScalarAsync<string>(cs, """SELECT "PhoneKey" FROM customers WHERE "FullName" = 'Juan Pérez'""")).Should().Be("23456789");
            (await ScalarAsync<Guid>(cs, """SELECT "ShopId" FROM users WHERE "Email" = 'viejo@local'""")).Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            (await ScalarAsync<long>(cs, """SELECT count(*) FROM order_notes""")).Should().Be(0);
            (await ScalarAsync<int>(cs, """SELECT "Status" FROM notification_outbox""")).Should().Be(4, "old queued messages must not be sent after the upgrade");
            (await ScalarAsync<bool>(cs, """SELECT "WarrantyExpiresAtUtc" IS NOT NULL FROM repair_orders WHERE "Id" = 'eeeeeeee-0000-0000-0000-000000000001'""")).Should().BeTrue();

            await using (var db = NewContext(cs))
                (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }
        finally
        {
            TestDatabase.Drop(cs);
        }
    }

    private static RepairShopDbContext NewContext(string cs)
        => new(new DbContextOptionsBuilder<RepairShopDbContext>().UseNpgsql(cs).Options);

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(string cs, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (T)(await cmd.ExecuteScalarAsync())!;
    }
}
