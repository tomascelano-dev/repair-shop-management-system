using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Files;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Users;

namespace RepairShop.Infrastructure.Persistence.Configurations;

internal sealed class RepairOrderConfiguration : IEntityTypeConfiguration<RepairOrder>
{
    public void Configure(EntityTypeBuilder<RepairOrder> b)
    {
        b.ToTable("repair_orders");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasRowVersion();

        b.Property(x => x.OrderNumber).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.OrderNumber }).IsUnique();

        b.Property(x => x.CustomerId).IsRequired();
        b.Property(x => x.DeviceId).IsRequired();
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ShopId, x.CustomerId });
        b.HasIndex(x => new { x.ShopId, x.DeviceId });
        b.HasIndex(x => new { x.ShopId, x.Status });
        b.HasIndex(x => new { x.ShopId, x.AssignedTechnicianId });
        b.HasIndex(x => new { x.ShopId, x.CreatedAtUtc });

        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AssignedTechnicianId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.WarrantyOfOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.ReceptionSignatureFileId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.DeliverySignatureFileId).OnDelete(DeleteBehavior.SetNull);

        b.Property(x => x.PublicToken).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.PublicToken).IsUnique();

        b.Property(x => x.IssueDescription).HasMaxLength(500).IsRequired();
        b.Property(x => x.IssueCategory).HasMaxLength(60);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.Status).IsRequired();
        b.Property(x => x.CancellationReason).HasMaxLength(300);
        b.Property(x => x.UnlockSecretProtected).HasMaxLength(2000);
        b.Property(x => x.ReceptionSignedByName).HasMaxLength(120);
        b.Property(x => x.DeliverySignedByName).HasMaxLength(120);

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.Property(x => x.QuoteAmount).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.QuoteCurrency).HasMaxLength(8);

        b.Ignore(x => x.Code);
        b.Ignore(x => x.IsFinal);
    }
}

internal sealed class RepairOrderStatusHistoryConfiguration : IEntityTypeConfiguration<RepairOrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<RepairOrderStatusHistory> b)
    {
        b.ToTable("order_status_history");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.RepairOrderId).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.RepairOrderId });
        b.HasIndex(x => new { x.ShopId, x.ChangedAtUtc });
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.FromStatus).IsRequired();
        b.Property(x => x.ToStatus).IsRequired();
        b.Property(x => x.ChangedByUserId).IsRequired();
        b.Property(x => x.ChangedAtUtc).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(300);
    }
}

internal sealed class RepairOrderNoteConfiguration : IEntityTypeConfiguration<RepairOrderNote>
{
    public void Configure(EntityTypeBuilder<RepairOrderNote> b)
    {
        b.ToTable("order_notes");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.RepairOrderId).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.RepairOrderId });
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Body).HasMaxLength(1200).IsRequired();
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}

internal sealed class RepairOrderAttachmentConfiguration : IEntityTypeConfiguration<RepairOrderAttachment>
{
    public void Configure(EntityTypeBuilder<RepairOrderAttachment> b)
    {
        b.ToTable("order_attachments");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.RepairOrderId).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.RepairOrderId });
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Url).HasMaxLength(800);
        b.Property(x => x.Label).HasMaxLength(120);
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
    }
}

internal sealed class RepairOrderPaymentConfiguration : IEntityTypeConfiguration<RepairOrderPayment>
{
    public void Configure(EntityTypeBuilder<RepairOrderPayment> b)
    {
        b.ToTable("order_payments");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.RepairOrderId).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.RepairOrderId });
        b.HasIndex(x => new { x.ShopId, x.CreatedAtUtc });
        b.HasIndex(x => x.CashSessionId);
        b.HasIndex(x => new { x.ShopId, x.ExternalPaymentId }).IsUnique().HasFilter("\"ExternalPaymentId\" IS NOT NULL");
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RepairOrderPayment>().WithMany().HasForeignKey(x => x.RefundOfPaymentId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Amount).HasColumnType(ConfigurationExtensions.MoneyType).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        b.Property(x => x.Method).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(120);
        b.Property(x => x.ExternalPaymentId).HasMaxLength(80);
        b.Property(x => x.CreatedByUserId).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Ignore(x => x.SignedAmount);
    }
}

internal sealed class RepairOrderReceptionChecklistConfiguration : IEntityTypeConfiguration<RepairOrderReceptionChecklist>
{
    public void Configure(EntityTypeBuilder<RepairOrderReceptionChecklist> b)
    {
        b.ToTable("order_reception_checklists");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.RepairOrderId).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.RepairOrderId }).IsUnique();
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.CosmeticNotes).HasMaxLength(500);
        b.Property(x => x.UpdatedByUserId).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}

internal sealed class RepairOrderQaChecklistConfiguration : IEntityTypeConfiguration<RepairOrderQaChecklist>
{
    public void Configure(EntityTypeBuilder<RepairOrderQaChecklist> b)
    {
        b.ToTable("order_qa_checklists");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasIndex(x => new { x.ShopId, x.RepairOrderId }).IsUnique();
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Notes).HasMaxLength(1000);
    }
}

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> b)
    {
        b.ToTable("quotes");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasRowVersion();
        b.HasIndex(x => new { x.RepairOrderId, x.Version }).IsUnique();
        b.HasIndex(x => new { x.ShopId, x.Status });
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Subtotal).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.DiscountAmount).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.Total).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.DecisionNote).HasMaxLength(500);
        b.Property(x => x.DecisionIp).HasMaxLength(64);

        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.QuoteId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.IsOpen);
    }
}

internal sealed class QuoteItemConfiguration : IEntityTypeConfiguration<QuoteItem>
{
    public void Configure(EntityTypeBuilder<QuoteItem> b)
    {
        b.ToTable("quote_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.Quantity).HasColumnType("numeric(18,3)");
        b.Property(x => x.UnitPrice).HasColumnType(ConfigurationExtensions.MoneyType);
        b.Property(x => x.LineTotal).HasColumnType(ConfigurationExtensions.MoneyType);
        b.HasIndex(x => x.InventoryItemId);
    }
}
