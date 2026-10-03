using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairShop.Domain.Currency;
using RepairShop.Domain.Files;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;

namespace RepairShop.Infrastructure.Persistence.Configurations;

internal sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> b)
    {
        b.ToTable("shops");
        b.HasKey(x => x.Id);
        b.Property(x => x.OrganizationId).IsRequired();
        b.HasIndex(x => x.OrganizationId);

        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.AddressLine).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(80);
        b.Property(x => x.Country).HasMaxLength(80);
        b.Property(x => x.IsActive).IsRequired();

        b.Property(x => x.LegalName).HasMaxLength(160);
        b.Property(x => x.TaxId).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(180);
        b.Property(x => x.DefaultCurrency).HasMaxLength(3).IsRequired().HasDefaultValue("ARS");
        b.Property(x => x.ReportingCurrency).HasMaxLength(3).IsRequired().HasDefaultValue("ARS");
        b.Property(x => x.PhoneCountryCode).HasMaxLength(4).IsRequired().HasDefaultValue("54");
        b.Property(x => x.TimeZone).HasMaxLength(64).IsRequired().HasDefaultValue(Shop.DefaultTimeZone);
        b.Property(x => x.ReceptionTerms).HasMaxLength(4000);
        b.Property(x => x.WarrantyTerms).HasMaxLength(4000);
        b.Property(x => x.PickupHours).HasMaxLength(200);
        b.Property(x => x.GoogleReviewUrl).HasMaxLength(500);
        b.Property(x => x.ReadyReminderDays).HasMaxLength(60).IsRequired().HasDefaultValue("7,15,30");

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
        b.HasIndex(x => x.Name);
    }
}

internal sealed class ShopIntegrationConfiguration : IEntityTypeConfiguration<ShopIntegration>
{
    public void Configure(EntityTypeBuilder<ShopIntegration> b)
    {
        b.ToTable("shop_integrations");
        b.HasKey(x => x.ShopId);
        b.HasOne<Shop>().WithOne().HasForeignKey<ShopIntegration>(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.MercadoPagoAccessTokenProtected).HasMaxLength(4000);
        b.Property(x => x.MercadoPagoWebhookSecretProtected).HasMaxLength(4000);
        b.Property(x => x.FiscalCertificateProtected).HasMaxLength(40000);
        b.Property(x => x.FiscalCertificatePasswordProtected).HasMaxLength(4000);
    }
}

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);

        b.Property(x => x.ShopId).IsRequired();
        b.HasIndex(x => x.ShopId);
        b.HasOne<Shop>().WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.Email).HasMaxLength(180).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();

        b.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        b.Property(x => x.Role).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(400).IsRequired();
        b.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
        b.Property(x => x.PendingTokenHash).HasMaxLength(128);
        b.HasIndex(x => x.PendingTokenHash);
        b.Property(x => x.EmailVerificationTokenHash).HasMaxLength(128);
        b.HasIndex(x => x.EmailVerificationTokenHash);

        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.FamilyId });
        b.HasIndex(x => x.ExpiresAtUtc);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Shop>().WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.RevokedReason).HasMaxLength(80);
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(300);
    }
}

internal sealed class UserShopAccessConfiguration : IEntityTypeConfiguration<UserShopAccess>
{
    public void Configure(EntityTypeBuilder<UserShopAccess> b)
    {
        b.ToTable("user_shop_access");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.ShopId }).IsUnique();
        b.HasIndex(x => x.ShopId);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Shop>().WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ShopCounterConfiguration : IEntityTypeConfiguration<ShopCounter>
{
    public void Configure(EntityTypeBuilder<ShopCounter> b)
    {
        b.ToTable("shop_counters");
        b.HasKey(x => new { x.ScopeId, x.Key });
        b.Property(x => x.Key).HasMaxLength(60);
    }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.ExpiresAtUtc);
    }
}

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b)
    {
        b.ToTable("stored_files");
        b.HasKey(x => x.Id);
        b.BelongsToShop();
        b.Property(x => x.StorageKey).HasMaxLength(400).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(160).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        b.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.Purpose).HasMaxLength(40).IsRequired();
    }
}

internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> b)
    {
        b.ToTable("exchange_rates");
        b.HasKey(x => x.Id);
        b.Property(x => x.BaseCurrency).HasMaxLength(3).IsRequired();
        b.Property(x => x.QuoteCurrency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Source).HasMaxLength(30).IsRequired();
        b.Property(x => x.Rate).HasColumnType("numeric(18,6)");
        b.HasIndex(x => new { x.Date, x.BaseCurrency, x.QuoteCurrency, x.Source }).IsUnique();
    }
}

internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> b)
    {
        b.ToTable("data_protection_keys");
    }
}
