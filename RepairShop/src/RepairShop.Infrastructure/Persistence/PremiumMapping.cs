using Microsoft.EntityFrameworkCore;
using RepairShop.Domain.Premium;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Infrastructure.Persistence;

public sealed partial class RepairShopDbContext
{
    public DbSet<RefurbEvent> RefurbEvents => Set<RefurbEvent>();
    public DbSet<PremiumBranch> PremiumBranches => Set<PremiumBranch>();
    public DbSet<SupplierPrice> SupplierPrices => Set<SupplierPrice>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockMinimum> StockMinimums => Set<StockMinimum>();
    public DbSet<OrderExpense> OrderExpenses => Set<OrderExpense>();
    public DbSet<WarrantyCase> WarrantyCases => Set<WarrantyCase>();
    public DbSet<RefurbDevice> RefurbDevices => Set<RefurbDevice>();
    public DbSet<RefurbExpense> RefurbExpenses => Set<RefurbExpense>();
    public DbSet<CompanyContract> CompanyContracts => Set<CompanyContract>();
    public DbSet<CompanyEquipment> CompanyEquipments => Set<CompanyEquipment>();
    public DbSet<OrderBusiness> OrderBusinesses => Set<OrderBusiness>();
    public DbSet<ContractSettlement> ContractSettlements => Set<ContractSettlement>();
    private static void ConfigurePremium(ModelBuilder m)
    {
        Configure<RefurbEvent>(m, "premium_refurb_event");
        m.Entity<RefurbEvent>().HasOne<RefurbDevice>().WithMany().HasForeignKey(x => new { x.ShopId, x.DeviceId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<InventoryItem>().HasAlternateKey(x => new { x.ShopId, x.Id });
        m.Entity<WorkshopWorkflow>().HasAlternateKey(x => new { x.ShopId, x.Id });
        Configure<PremiumBranch>(m, "premium_premium_branch");
        Configure<SupplierPrice>(m, "premium_supplier_price");
        Configure<StockLot>(m, "premium_stock_lot");
        Configure<StockReservation>(m, "premium_stock_reservation");
        Configure<StockMovement>(m, "premium_stock_movement");
        Configure<StockMinimum>(m, "premium_stock_minimum");
        Configure<OrderExpense>(m, "premium_order_expense");
        Configure<WarrantyCase>(m, "premium_warranty_case");
        Configure<RefurbDevice>(m, "premium_refurb_device");
        Configure<RefurbExpense>(m, "premium_refurb_expense");
        Configure<CompanyContract>(m, "premium_company_contract");
        Configure<CompanyEquipment>(m, "premium_company_equipment");
        Configure<OrderBusiness>(m, "premium_order_business");
        Configure<ContractSettlement>(m, "premium_contract_settlement");
        m.Entity<StockLot>().HasOne<PremiumBranch>().WithMany().HasForeignKey(x => new { x.ShopId, x.BranchId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<StockLot>().HasOne<InventoryItem>().WithMany().HasForeignKey(x => new { x.ShopId, x.ItemId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<StockMinimum>().HasOne<PremiumBranch>().WithMany().HasForeignKey(x => new { x.ShopId, x.BranchId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<StockMinimum>().HasOne<InventoryItem>().WithMany().HasForeignKey(x => new { x.ShopId, x.ItemId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<StockReservation>().HasOne<StockLot>().WithMany().HasForeignKey(x => new { x.ShopId, x.LotId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<StockReservation>().HasOne<WorkshopWorkflow>().WithMany().HasForeignKey(x => new { x.ShopId, x.OrderId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<StockMovement>().HasOne<StockLot>().WithMany().HasForeignKey(x => new { x.ShopId, x.LotId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderExpense>().HasOne<WorkshopWorkflow>().WithMany().HasForeignKey(x => new { x.ShopId, x.OrderId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WarrantyCase>().HasOne<WorkshopWorkflow>().WithMany().HasForeignKey(x => new { x.ShopId, x.OrderId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<WarrantyCase>().HasOne<StockReservation>().WithMany().HasForeignKey(x => new { x.ShopId, x.ReservationId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RefurbDevice>().HasOne<PremiumBranch>().WithMany().HasForeignKey(x => new { x.ShopId, x.BranchId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RefurbExpense>().HasOne<RefurbDevice>().WithMany().HasForeignKey(x => new { x.ShopId, x.DeviceId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CompanyEquipment>().HasOne<CompanyContract>().WithMany().HasForeignKey(x => new { x.ShopId, x.ContractId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderBusiness>().HasOne<WorkshopWorkflow>().WithMany().HasForeignKey(x => new { x.ShopId, x.OrderId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderBusiness>().HasOne<PremiumBranch>().WithMany().HasForeignKey(x => new { x.ShopId, x.BranchId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderBusiness>().HasOne<CompanyContract>().WithMany().HasForeignKey(x => new { x.ShopId, x.ContractId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<OrderBusiness>().HasOne<CompanyEquipment>().WithMany().HasForeignKey(x => new { x.ShopId, x.EquipmentId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ContractSettlement>().HasOne<CompanyContract>().WithMany().HasForeignKey(x => new { x.ShopId, x.ContractId }).HasPrincipalKey(x => new { x.ShopId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        m.Entity<PremiumBranch>().HasIndex(x => new { x.ShopId, x.Name }).IsUnique();
        m.Entity<SupplierPrice>().HasIndex(x => new { x.ShopId, x.Supplier, x.Sku }).IsUnique();
        m.Entity<StockMinimum>().HasIndex(x => new { x.ShopId, x.ItemId, x.BranchId }).IsUnique();
        m.Entity<OrderBusiness>().HasIndex(x => new { x.ShopId, x.OrderId }).IsUnique();
        m.Entity<CompanyEquipment>().HasIndex(x => new { x.ShopId, x.ContractId, x.Identifier }).IsUnique();
        m.Entity<RefurbDevice>().HasIndex(x => new { x.ShopId, x.Identifier }).IsUnique();
        m.Entity<ContractSettlement>().HasIndex(x => new { x.ShopId, x.ContractId, x.Period }).IsUnique();
        m.Entity<StockLot>().HasIndex(x => new { x.ShopId, x.Serial }).IsUnique().HasFilter("\"Serial\" IS NOT NULL");
    }
    private static void Configure<T>(ModelBuilder m, string table) where T : class, IPremiumRecord
    {
        var b = m.Entity<T>(); b.ToTable(table); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.ShopId, x.Id });
        b.HasIndex(x => x.ShopId);
        foreach (var p in typeof(T).GetProperties())
        {
            if (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)) b.Property(p.Name).HasPrecision(18,2);
            if (p.PropertyType == typeof(string)) b.Property(p.Name).HasMaxLength(p.Name.EndsWith("Json") ? 20000 : 2000);
        }
    }
}
