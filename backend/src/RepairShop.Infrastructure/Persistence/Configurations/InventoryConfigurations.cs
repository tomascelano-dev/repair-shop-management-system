using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Shops;

namespace RepairShop.Infrastructure.Persistence.Configurations;

internal sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> b)
    {
        b.ToTable("inventory_items");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasRowVersion();

        b.Property(x => x.Sku).HasMaxLength(60).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.Sku }).IsUnique();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Category).HasMaxLength(60);
        b.Property(x => x.Barcode).HasMaxLength(64);
        b.HasIndex(x => new { x.ShopId, x.Barcode }).IsUnique().HasFilter("\"Barcode\" IS NOT NULL");
        b.Property(x => x.QuantityOnHand).IsRequired();
        b.Property(x => x.UnitCost).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.UnitCostCurrency).HasMaxLength(8);
        b.Property(x => x.SalePrice).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.SalePriceCurrency).HasMaxLength(8);
        b.Property(x => x.Location).HasMaxLength(60);
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}

internal sealed class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> b)
    {
        b.ToTable("inventory_adjustments");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.InventoryItemId).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.InventoryItemId });
        b.HasIndex(x => new { x.ShopId, x.CreatedAtUtc });
        b.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Type).IsRequired();
        b.Property(x => x.DeltaQuantity).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(300);
        b.Property(x => x.ReferenceType).HasMaxLength(40);
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}

internal sealed class RepairOrderPartUsageConfiguration : IEntityTypeConfiguration<RepairOrderPartUsage>
{
    public void Configure(EntityTypeBuilder<RepairOrderPartUsage> b)
    {
        b.ToTable("order_part_usage");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.RepairOrderId).IsRequired();
        b.Property(x => x.InventoryItemId).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.RepairOrderId });
        b.HasIndex(x => new { x.ShopId, x.InventoryItemId });
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.QuantityUsed).IsRequired();
        b.Property(x => x.UnitPrice).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.UnitPriceCurrency).HasMaxLength(8);
        b.Property(x => x.UnitCost).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.UnitCostCurrency).HasMaxLength(8);
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Ignore(x => x.ExtraCharge);
    }
}

internal sealed class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> b)
    {
        b.ToTable("inventory_reservations");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasRowVersion();
        b.HasIndex(x => new { x.ShopId, x.InventoryItemId, x.Status });
        b.HasIndex(x => new { x.ShopId, x.RepairOrderId });
        b.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Quote>().WithMany().HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.SetNull);
        b.Ignore(x => x.RemainingQuantity);
    }
}

internal sealed class InventoryItemCompatibilityConfiguration : IEntityTypeConfiguration<InventoryItemCompatibility>
{
    public void Configure(EntityTypeBuilder<InventoryItemCompatibility> b)
    {
        b.ToTable("inventory_item_compatibilities");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Brand).HasMaxLength(60).IsRequired();
        b.Property(x => x.Model).HasMaxLength(60).IsRequired();
        b.Property(x => x.BrandKey).HasMaxLength(60).IsRequired();
        b.Property(x => x.ModelKey).HasMaxLength(60).IsRequired();
        b.HasIndex(x => new { x.InventoryItemId, x.BrandKey, x.ModelKey }).IsUnique();
        b.HasIndex(x => new { x.ShopId, x.BrandKey, x.ModelKey });
    }
}

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("suppliers");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.ContactName).HasMaxLength(120);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(180);
        b.Property(x => x.TaxId).HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.ShopId, x.Name });
    }
}

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.ToTable("purchase_orders");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasRowVersion();
        b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.ShopId, x.Status });
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.Total).HasColumnType(ConfigurationExtensions.MoneyType);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.Code);
    }
}

internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> b)
    {
        b.ToTable("purchase_order_lines");
        b.HasKey(x => x.Id);
        b.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.UnitCost).HasColumnType(ConfigurationExtensions.MoneyType);
    }
}

internal sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> b)
    {
        b.ToTable("stock_transfers");
        b.HasKey(x => x.Id);
        b.HasRowVersion();
        b.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
        b.HasIndex(x => x.FromShopId);
        b.HasIndex(x => x.ToShopId);
        b.HasOne<Shop>().WithMany().HasForeignKey(x => x.FromShopId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Shop>().WithMany().HasForeignKey(x => x.ToShopId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.StockTransferId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.Code);
    }
}

internal sealed class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> b)
    {
        b.ToTable("stock_transfer_lines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Sku).HasMaxLength(60).IsRequired();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.UnitCost).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.UnitCostCurrency).HasMaxLength(8);
    }
}
