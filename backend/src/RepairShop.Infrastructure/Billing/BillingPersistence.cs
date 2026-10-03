using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Billing;
using RepairShop.Infrastructure.Persistence;
using RepairShop.Infrastructure.Persistence.Configurations;

namespace RepairShop.Infrastructure.Billing;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.ToTable("subscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.OrganizationId).IsRequired();
        b.HasIndex(x => x.OrganizationId).IsUnique();
        b.Property(x => x.Plan).IsRequired();
        b.Property(x => x.Status).IsRequired();
        b.Property(x => x.Provider).IsRequired();
        b.Property(x => x.BillingCountry).HasMaxLength(2).IsRequired();
        b.Property(x => x.ProviderSubscriptionId).HasMaxLength(80);
        b.HasIndex(x => new { x.Provider, x.ProviderSubscriptionId });
        b.Property(x => x.ProviderCustomerId).HasMaxLength(80);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.TrialEndsAtUtc).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
        b.HasRowVersion(); // webhooks and checkouts can race
        b.Ignore(x => x.PlanDefinition);
        b.Ignore(x => x.HasLiveProviderSubscription);
    }
}

internal sealed class SignupAttributionConfiguration : IEntityTypeConfiguration<SignupAttribution>
{
    public void Configure(EntityTypeBuilder<SignupAttribution> b)
    {
        b.ToTable("signup_attributions");
        b.HasKey(x => x.OrganizationId);
        b.Property(x => x.Source).HasMaxLength(120);
        b.Property(x => x.Medium).HasMaxLength(120);
        b.Property(x => x.Campaign).HasMaxLength(200);
        b.Property(x => x.Term).HasMaxLength(200);
        b.Property(x => x.Content).HasMaxLength(200);
        b.Property(x => x.Gclid).HasMaxLength(300);
        b.Property(x => x.Gbraid).HasMaxLength(300);
        b.Property(x => x.Wbraid).HasMaxLength(300);
        b.Property(x => x.Fbclid).HasMaxLength(300);
        b.Property(x => x.Fbp).HasMaxLength(300);
        b.Property(x => x.Fbc).HasMaxLength(400);
        b.Property(x => x.LandingPath).HasMaxLength(300);
        b.Property(x => x.Referrer).HasMaxLength(500);
        b.Property(x => x.TrialEventId).HasMaxLength(64);
        b.Property(x => x.ClientIp).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(300);
        b.HasIndex(x => x.CreatedAtUtc);
        b.Ignore(x => x.FromCampaign);
    }
}

internal sealed class AdConversionConfiguration : IEntityTypeConfiguration<AdConversion>
{
    public void Configure(EntityTypeBuilder<AdConversion> b)
    {
        b.ToTable("ad_conversions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Platform).HasMaxLength(20).IsRequired();
        b.Property(x => x.EventName).HasMaxLength(40).IsRequired();
        b.Property(x => x.EventId).HasMaxLength(120).IsRequired();
        b.Property(x => x.Payload).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(500);
        b.HasIndex(x => new { x.Platform, x.EventId });
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
    }
}

internal sealed class AdTrackingRepository : IAdTrackingRepository
{
    private readonly RepairShopDbContext _db;

    public AdTrackingRepository(RepairShopDbContext db) => _db = db;

    public Task<SignupAttribution?> GetAttributionAsync(Guid organizationId, CancellationToken ct)
        => _db.SignupAttributions.FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct);

    public async Task AddAttributionAsync(SignupAttribution attribution, CancellationToken ct) => await _db.SignupAttributions.AddAsync(attribution, ct);

    public Task<bool> ConversionExistsAsync(string platform, string eventId, CancellationToken ct)
        => _db.AdConversions.AnyAsync(x => x.Platform == platform && x.EventId == eventId, ct);

    public async Task AddConversionAsync(AdConversion conversion, CancellationToken ct) => await _db.AdConversions.AddAsync(conversion, ct);
}

internal sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly RepairShopDbContext _db;

    public SubscriptionRepository(RepairShopDbContext db) => _db = db;

    public Task<Subscription?> GetByOrganizationAsync(Guid organizationId, CancellationToken ct)
        => _db.Subscriptions.FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct);

    public Task<Subscription?> GetByProviderIdAsync(BillingProvider provider, string providerSubscriptionId, CancellationToken ct)
        => _db.Subscriptions.FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderSubscriptionId == providerSubscriptionId, ct);

    public async Task AddAsync(Subscription subscription, CancellationToken ct) => await _db.Subscriptions.AddAsync(subscription, ct);
}

internal sealed class ShopProvisioner : IShopProvisioner
{
    private readonly RepairShopDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ShopProvisioner(RepairShopDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task ProvisionAsync(Guid shopId, CancellationToken ct) => DbSeeder.SeedTemplatesAsync(_db, shopId, _clock.UtcNow, ct);
}
