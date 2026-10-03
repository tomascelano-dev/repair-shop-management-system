using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Feedback;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers");
        b.HasKey(x => x.Id);
        b.BelongsToShop();

        b.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(40).IsRequired();
        b.Property(x => x.PhoneKey).HasMaxLength(16).IsRequired().HasDefaultValue("");
        b.Property(x => x.Email).HasMaxLength(180);
        b.Property(x => x.DocumentNumber).HasMaxLength(20);
        b.Property(x => x.Address).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.Tags).HasMaxLength(300);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.ShopId, x.Phone });
        b.HasIndex(x => new { x.ShopId, x.PhoneKey });
        b.HasIndex(x => new { x.ShopId, x.Email });
    }
}

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.ToTable("devices");
        b.HasKey(x => x.Id);
        b.BelongsToShop();

        b.Property(x => x.CustomerId).IsRequired();
        b.HasIndex(x => new { x.ShopId, x.CustomerId });
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.Brand).HasMaxLength(60).IsRequired();
        b.Property(x => x.Model).HasMaxLength(60).IsRequired();
        b.Property(x => x.Label).HasMaxLength(120);
        b.Property(x => x.SerialNumber).HasMaxLength(80);
        b.Property(x => x.Imei).HasMaxLength(15);
        b.HasIndex(x => new { x.ShopId, x.Imei });
        b.Property(x => x.Notes).HasMaxLength(500);

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}

internal sealed class CustomerFeedbackConfiguration : IEntityTypeConfiguration<CustomerFeedback>
{
    public void Configure(EntityTypeBuilder<CustomerFeedback> b)
    {
        b.ToTable("customer_feedback");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.HasIndex(x => x.RepairOrderId).IsUnique();
        b.HasIndex(x => new { x.ShopId, x.CreatedAtUtc });
        b.HasOne<RepairOrder>().WithMany().HasForeignKey(x => x.RepairOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Comment).HasMaxLength(1000);
    }
}
