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
