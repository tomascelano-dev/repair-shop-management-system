namespace RepairShop.Infrastructure.Persistence.Migrations;

/// <summary>
/// Data fixes run by the PlatformModules migration when upgrading a database created by InitialCreate.
/// On a fresh database every statement is a no-op. Requires PostgreSQL 13+ (gen_random_uuid).
/// </summary>
internal static class PlatformModulesUpgradeSql
{
    private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";

    private static readonly string[] ShopScopedTables =
    {
        "users", "customers", "devices", "repair_orders", "order_status_history", "order_notes", "order_attachments",
        "order_payments", "order_reception_checklists", "message_templates", "audit_events", "notification_outbox",
        "inventory_items", "inventory_adjustments", "order_part_usage"
    };

    private static readonly string[] OrderChildTables =
    {
        "order_status_history", "order_notes", "order_attachments", "order_payments", "order_reception_checklists", "order_part_usage"
    };

    /// <summary>
    /// Runs on the legacy schema, before columns and foreign keys are added: assigns rows created without a shop,
    /// aligns child rows with their order's shop and creates placeholders for missing parents so the new foreign
    /// keys can be created without losing payments or stock movements.
    /// </summary>
    public static string BeforeSchemaChanges
    {
        get
        {
            var sql = new System.Text.StringBuilder();
            const string firstShop = "(SELECT \"Id\" FROM shops ORDER BY \"CreatedAtUtc\", \"Id\" LIMIT 1)";

            sql.AppendLine("UPDATE notification_outbox SET \"Title\" = '' WHERE \"Title\" IS NULL;");

            // Rows saved without a shop (legacy constructors) belong to the oldest shop.
            sql.AppendLine($"""
                DELETE FROM message_templates t
                WHERE t."ShopId" = '{EmptyGuid}'
                  AND EXISTS (SELECT 1 FROM message_templates o WHERE o."ShopId" = {firstShop} AND o."Key" = t."Key");
                """);
            foreach (var table in ShopScopedTables)
                sql.AppendLine($"UPDATE {table} SET \"ShopId\" = {firstShop} WHERE \"ShopId\" = '{EmptyGuid}' AND EXISTS (SELECT 1 FROM shops);");

            // Child rows live in the shop of their order.
            sql.AppendLine("""
                DELETE FROM order_reception_checklists c
                USING repair_orders o
                WHERE o."Id" = c."RepairOrderId" AND c."ShopId" <> o."ShopId"
                  AND EXISTS (SELECT 1 FROM order_reception_checklists x WHERE x."RepairOrderId" = c."RepairOrderId" AND x."ShopId" = o."ShopId");
                """);
            foreach (var table in OrderChildTables)
                sql.AppendLine($"UPDATE {table} c SET \"ShopId\" = o.\"ShopId\" FROM repair_orders o WHERE o.\"Id\" = c.\"RepairOrderId\" AND c.\"ShopId\" <> o.\"ShopId\";");

            // Placeholder shops for rows pointing to a shop that no longer exists (kept inactive).
            sql.AppendLine($"""
                INSERT INTO shops ("Id", "Name", "Phone", "AddressLine", "City", "Country", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT DISTINCT x."ShopId", 'Sucursal recuperada', NULL, NULL, NULL, NULL, false, now(), now()
                FROM ({string.Join(" UNION ", ShopScopedTables.Select(t => $"SELECT \"ShopId\" FROM {t}"))}) x
                WHERE NOT EXISTS (SELECT 1 FROM shops s WHERE s."Id" = x."ShopId");
                """);

            // Notes, history, photos and checklists of deleted orders have no value without the order.
            foreach (var table in new[] { "order_status_history", "order_notes", "order_attachments", "order_reception_checklists" })
                sql.AppendLine($"DELETE FROM {table} c WHERE NOT EXISTS (SELECT 1 FROM repair_orders o WHERE o.\"Id\" = c.\"RepairOrderId\");");

            // Payments and parts of deleted orders are kept under a cancelled placeholder order.
            sql.AppendLine("""
                CREATE TEMP TABLE rs_missing_orders ON COMMIT DROP AS
                SELECT DISTINCT ON (x."RepairOrderId") x."RepairOrderId", x."ShopId"
                FROM (SELECT "RepairOrderId", "ShopId" FROM order_payments UNION ALL SELECT "RepairOrderId", "ShopId" FROM order_part_usage) x
                WHERE NOT EXISTS (SELECT 1 FROM repair_orders o WHERE o."Id" = x."RepairOrderId");

                INSERT INTO customers ("Id", "ShopId", "FullName", "Phone", "Notes", "CreatedAtUtc")
                SELECT DISTINCT md5('rs-recovered-customer:' || m."ShopId")::uuid, m."ShopId", 'Cliente recuperado', '000000',
                       'Creado automáticamente al actualizar el sistema.', now()
                FROM rs_missing_orders m
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO devices ("Id", "ShopId", "CustomerId", "Brand", "Model", "Label", "SerialNumber", "Notes", "CreatedAtUtc")
                SELECT DISTINCT md5('rs-recovered-device:' || m."ShopId")::uuid, m."ShopId", md5('rs-recovered-customer:' || m."ShopId")::uuid,
                       'Desconocido', 'Desconocido', NULL, NULL, 'Creado automáticamente al actualizar el sistema.', now()
                FROM rs_missing_orders m
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO repair_orders ("Id", "ShopId", "CustomerId", "DeviceId", "IssueDescription", "Notes", "Status",
                                           "QuoteAmount", "QuoteCurrency", "QuoteUpdatedByUserId", "QuoteUpdatedAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT m."RepairOrderId", m."ShopId", md5('rs-recovered-customer:' || m."ShopId")::uuid, md5('rs-recovered-device:' || m."ShopId")::uuid,
                       'Orden recuperada (datos incompletos)', 'Creada automáticamente al actualizar: faltaba la orden original de estos pagos o repuestos.',
                       5, NULL, NULL, NULL, NULL, now(), now()
                FROM rs_missing_orders m;
                """);

            // Customers and devices referenced by orders/devices but missing.
            sql.AppendLine("""
                INSERT INTO customers ("Id", "ShopId", "FullName", "Phone", "Notes", "CreatedAtUtc")
                SELECT DISTINCT ON (m."CustomerId") m."CustomerId", m."ShopId", 'Cliente recuperado', '000000',
                       'Creado automáticamente al actualizar: faltaba el cliente original.', now()
                FROM (SELECT "CustomerId", "ShopId" FROM devices UNION ALL SELECT "CustomerId", "ShopId" FROM repair_orders) m
                WHERE NOT EXISTS (SELECT 1 FROM customers c WHERE c."Id" = m."CustomerId");

                INSERT INTO devices ("Id", "ShopId", "CustomerId", "Brand", "Model", "Label", "SerialNumber", "Notes", "CreatedAtUtc")
                SELECT DISTINCT ON (o."DeviceId") o."DeviceId", o."ShopId", o."CustomerId", 'Desconocido', 'Desconocido', NULL, NULL,
                       'Creado automáticamente al actualizar: faltaba el equipo original.', now()
                FROM repair_orders o
                WHERE NOT EXISTS (SELECT 1 FROM devices d WHERE d."Id" = o."DeviceId");

                INSERT INTO inventory_items ("Id", "ShopId", "Sku", "Name", "QuantityOnHand", "UnitCost", "UnitCostCurrency", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT DISTINCT ON (x."InventoryItemId") x."InventoryItemId", x."ShopId",
                       'RECUP-' || upper(substr(replace(x."InventoryItemId"::text, '-', ''), 1, 12)), 'Ítem recuperado', 0, NULL, NULL, false, now(), now()
                FROM (SELECT "InventoryItemId", "ShopId" FROM inventory_adjustments UNION ALL SELECT "InventoryItemId", "ShopId" FROM order_part_usage) x
                WHERE NOT EXISTS (SELECT 1 FROM inventory_items i WHERE i."Id" = x."InventoryItemId");
                """);

            return sql.ToString();
        }
    }

    /// <summary>Runs after the new columns exist: sensible values for legacy rows (the column defaults are only for the DDL).</summary>
    public const string BackfillNewColumns = """
        UPDATE users SET "IsActive" = true, "UpdatedAtUtc" = "CreatedAtUtc", "SecurityStamp" = replace(gen_random_uuid()::text, '-', '');

        UPDATE shops SET "OrganizationId" = "Id", "DefaultWarrantyDays" = 90, "QuoteValidityDays" = 7, "StaleOrderDays" = 5,
                         "NotificationsEnabled" = true, "SendFeedbackSurvey" = true, "RequireOpenCashSession" = true, "TaxCondition" = 2;

        UPDATE customers SET "NotificationsOptIn" = true, "UpdatedAtUtc" = "CreatedAtUtc",
                             "PhoneKey" = right(regexp_replace("Phone", '\D', '', 'g'), 8);

        UPDATE devices SET "UpdatedAtUtc" = "CreatedAtUtc";

        UPDATE inventory_items SET "TrackStock" = true;

        UPDATE repair_orders SET "Priority" = 1,
                                 "PublicToken" = replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', '');

        WITH numbered AS (
            SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "ShopId" ORDER BY "CreatedAtUtc", "Id") AS n FROM repair_orders)
        UPDATE repair_orders r SET "OrderNumber" = numbered.n FROM numbered WHERE numbered."Id" = r."Id";

        UPDATE repair_orders r SET "LastStatusChangeAtUtc" = COALESCE(
            (SELECT max(h."ChangedAtUtc") FROM order_status_history h WHERE h."RepairOrderId" = r."Id"), r."UpdatedAtUtc");
        UPDATE repair_orders r SET "ReadyAtUtc" = COALESCE(
            (SELECT max(h."ChangedAtUtc") FROM order_status_history h WHERE h."RepairOrderId" = r."Id" AND h."ToStatus" = 3), r."UpdatedAtUtc")
        WHERE r."Status" IN (3, 4);
        UPDATE repair_orders r SET "DeliveredAtUtc" = COALESCE(
            (SELECT max(h."ChangedAtUtc") FROM order_status_history h WHERE h."RepairOrderId" = r."Id" AND h."ToStatus" = 4), r."UpdatedAtUtc")
        WHERE r."Status" = 4;
        UPDATE repair_orders r SET "CancelledAtUtc" = COALESCE(
            (SELECT max(h."ChangedAtUtc") FROM order_status_history h WHERE h."RepairOrderId" = r."Id" AND h."ToStatus" = 5), r."UpdatedAtUtc")
        WHERE r."Status" = 5;
        UPDATE repair_orders r SET "WarrantyDays" = s."DefaultWarrantyDays",
                                   "WarrantyExpiresAtUtc" = r."DeliveredAtUtc" + make_interval(days => s."DefaultWarrantyDays")
        FROM shops s
        WHERE s."Id" = r."ShopId" AND r."Status" = 4 AND r."DeliveredAtUtc" IS NOT NULL;

        -- Messages queued before automatic sending existed were never meant to be delivered now.
        UPDATE notification_outbox SET "Status" = 4, "LastError" = 'Cancelado al actualizar: mensaje anterior al envío automático.', "UpdatedAtUtc" = now()
        WHERE "Status" IN (0, 1, 3);
        """;

    /// <summary>Runs after the new tables exist: order numbering continues after the backfilled numbers.</summary>
    public const string SeedCounters = """
        INSERT INTO shop_counters ("ScopeId", "Key", "Value")
        SELECT "ShopId", 'repair_order', max("OrderNumber") FROM repair_orders GROUP BY "ShopId"
        ON CONFLICT ("ScopeId", "Key") DO UPDATE SET "Value" = GREATEST(shop_counters."Value", EXCLUDED."Value");
        """;
}
