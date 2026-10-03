using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairShop.Domain.Cash;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Fiscal;
using RepairShop.Domain.Payments;
using RepairShop.Domain.Sales;

namespace RepairShop.Infrastructure.Persistence.Configurations;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("sales");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasRowVersion();
        b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.ShopId, x.CreatedAtUtc });
        b.HasIndex(x => x.CashSessionId);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CashRegisterSession>().WithMany().HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        foreach (var p in new[] { nameof(Sale.Subtotal), nameof(Sale.DiscountAmount), nameof(Sale.Total), nameof(Sale.PaidAmount), nameof(Sale.ChangeAmount), nameof(Sale.RefundedAmount) })
            b.Property(p).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.VoidReason).HasMaxLength(300);

        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(p => p.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Refunds).WithOne().HasForeignKey(r => r.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Refunds).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.Code);
    }
}

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> b)
    {
        b.ToTable("sale_lines");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.InventoryItemId);
        b.Property(x => x.Sku).HasMaxLength(60);
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.UnitPrice).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.DiscountAmount).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.LineTotal).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.UnitCost).HasColumnType(ConfigurationExtensions.MoneyType);
    }
}

internal sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> b)
    {
        b.ToTable("sale_payments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.Reference).HasMaxLength(120);
    }
}

internal sealed class SaleRefundConfiguration : IEntityTypeConfiguration<SaleRefund>
{
    public void Configure(EntityTypeBuilder<SaleRefund> b)
    {
        b.ToTable("sale_refunds");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.Reason).HasMaxLength(300);
        b.HasIndex(x => x.CashSessionId);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SaleRefundId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SaleRefundLineConfiguration : IEntityTypeConfiguration<SaleRefundLine>
{
    public void Configure(EntityTypeBuilder<SaleRefundLine> b)
    {
        b.ToTable("sale_refund_lines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType(ConfigurationExtensions.MoneyType);
    }
}

internal sealed class CashRegisterSessionConfiguration : IEntityTypeConfiguration<CashRegisterSession>
{
    public void Configure(EntityTypeBuilder<CashRegisterSession> b)
    {
        b.ToTable("cash_sessions");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasRowVersion();
        b.HasIndex(x => new { x.ShopId, x.Number }).IsUnique();
        // Only one open session per shop.
        b.HasIndex(x => x.ShopId).IsUnique().HasFilter("\"Status\" = 0").HasDatabaseName("IX_cash_sessions_ShopId_open");
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        foreach (var p in new[] { nameof(CashRegisterSession.OpeningCash), nameof(CashRegisterSession.CountedCash), nameof(CashRegisterSession.ExpectedCash), nameof(CashRegisterSession.Difference) })
            b.Property(p).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.OpeningNotes).HasMaxLength(500);
        b.Property(x => x.ClosingNotes).HasMaxLength(1000);
        b.Property(x => x.ClosingSummaryJson).HasMaxLength(8000);
        b.Ignore(x => x.IsOpen);
    }
}

internal sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> b)
    {
        b.ToTable("cash_movements");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasIndex(x => x.SessionId);
        b.HasIndex(x => new { x.ShopId, x.CreatedAtUtc });
        b.HasOne<CashRegisterSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Amount).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.Category).HasMaxLength(60);
        b.Property(x => x.RelatedEntityType).HasMaxLength(40);
        b.Ignore(x => x.IsInflow);
        b.Ignore(x => x.SignedAmount);
    }
}

internal sealed class PaymentLinkConfiguration : IEntityTypeConfiguration<PaymentLink>
{
    public void Configure(EntityTypeBuilder<PaymentLink> b)
    {
        b.ToTable("payment_links");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasIndex(x => new { x.ShopId, x.EntityType, x.EntityId });
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(40).IsRequired();
        b.Property(x => x.Amount).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.ExternalId).HasMaxLength(120);
        b.Property(x => x.Url).HasMaxLength(800);
        b.Property(x => x.ExternalPaymentId).HasMaxLength(80);
    }
}

internal sealed class FiscalInvoiceConfiguration : IEntityTypeConfiguration<FiscalInvoice>
{
    public void Configure(EntityTypeBuilder<FiscalInvoice> b)
    {
        b.ToTable("fiscal_invoices");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasIndex(x => new { x.ShopId, x.SourceType, x.SourceId });
        b.HasIndex(x => new { x.ShopId, x.VoucherType, x.PointOfSale, x.Number }).IsUnique().HasFilter("\"Number\" IS NOT NULL");
        b.HasOne<FiscalInvoice>().WithMany().HasForeignKey(x => x.AssociatedInvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.SourceType).HasMaxLength(40).IsRequired();
        b.Property(x => x.ReceiverDocumentNumber).HasMaxLength(20);
        b.Property(x => x.ReceiverName).HasMaxLength(160).IsRequired();
        b.Property(x => x.NetAmount).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.VatAmount).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.Total).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Cae).HasMaxLength(20);
        b.Property(x => x.ResultMessage).HasMaxLength(2000);
        b.Ignore(x => x.IsCreditNote);
        b.Ignore(x => x.Code);
    }
}
