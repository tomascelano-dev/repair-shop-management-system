using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairShop.Domain.Common;
using RepairShop.Domain.Shops;

namespace RepairShop.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public const string MoneyType = "numeric(18,2)";

    /// <summary>ShopId required + index + FK to shops (restrict).</summary>
    public static EntityTypeBuilder<T> BelongsToShop<T>(this EntityTypeBuilder<T> b) where T : class, IShopScoped
    {
        b.Property(x => x.ShopId).IsRequired();
        b.HasIndex(x => x.ShopId);
        b.HasOne<Shop>().WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Restrict);
        return b;
    }

    /// <summary>Optimistic concurrency using PostgreSQL's xmin system column.</summary>
    public static EntityTypeBuilder<T> HasRowVersion<T>(this EntityTypeBuilder<T> b) where T : class
    {
        b.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
        return b;
    }
}
