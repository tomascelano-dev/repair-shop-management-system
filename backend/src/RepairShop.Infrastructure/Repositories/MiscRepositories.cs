using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Auditing;
using RepairShop.Domain.Currency;
using RepairShop.Domain.Files;
using RepairShop.Domain.Messaging;
using RepairShop.Domain.Notifications;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class StoredFileRepository : IStoredFileRepository
{
    private readonly RepairShopDbContext _db;
    public StoredFileRepository(RepairShopDbContext db) => _db = db;

    public Task<StoredFile?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.StoredFiles.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<StoredFile?> GetByIdAnyShopAsync(Guid id, CancellationToken ct)
        => _db.StoredFiles.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task AddAsync(StoredFile file, CancellationToken ct)
        => _db.StoredFiles.AddAsync(file, ct).AsTask();
}

public sealed class ExchangeRateRepository : IExchangeRateRepository
{
    private readonly RepairShopDbContext _db;
    public ExchangeRateRepository(RepairShopDbContext db) => _db = db;

    public Task<ExchangeRate?> GetAsync(DateOnly date, string baseCurrency, string quoteCurrency, string source, CancellationToken ct)
        => _db.ExchangeRates.FirstOrDefaultAsync(x => x.Date == date && x.BaseCurrency == baseCurrency && x.QuoteCurrency == quoteCurrency && x.Source == source, ct);

    public Task<ExchangeRate?> GetLatestOnOrBeforeAsync(DateOnly date, string baseCurrency, string quoteCurrency, string? source, CancellationToken ct)
        => _db.ExchangeRates
            .Where(x => x.Date <= date && x.BaseCurrency == baseCurrency && x.QuoteCurrency == quoteCurrency && (source == null || x.Source == source))
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<List<ExchangeRate>> ListRecentAsync(int take, CancellationToken ct)
        => _db.ExchangeRates.OrderByDescending(x => x.Date).ThenBy(x => x.Source).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);

    public Task<List<ExchangeRate>> ListRangeAsync(DateOnly from, DateOnly to, string baseCurrency, string quoteCurrency, CancellationToken ct)
        => _db.ExchangeRates
            .Where(x => x.Date >= from && x.Date <= to && x.BaseCurrency == baseCurrency && x.QuoteCurrency == quoteCurrency)
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

    public Task AddAsync(ExchangeRate rate, CancellationToken ct)
        => _db.ExchangeRates.AddAsync(rate, ct).AsTask();
}

public sealed class MessageTemplateRepository : IMessageTemplateRepository
{
    private readonly RepairShopDbContext _db;
    public MessageTemplateRepository(RepairShopDbContext db) => _db = db;

    public Task<MessageTemplate?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.MessageTemplates.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<MessageTemplate?> GetByKeyAsync(Guid shopId, string key, CancellationToken ct)
    {
        key = (key ?? "").Trim().ToLowerInvariant();
        return _db.MessageTemplates.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Key == key, ct);
    }

    public Task<List<MessageTemplate>> ListAsync(Guid shopId, bool includeInactive, CancellationToken ct)
    {
        var q = _db.MessageTemplates.Where(x => x.ShopId == shopId);
        if (!includeInactive) q = q.Where(x => x.IsActive);
        return q.OrderBy(x => x.Key).ToListAsync(ct);
    }

    public Task AddAsync(MessageTemplate template, CancellationToken ct)
        => _db.MessageTemplates.AddAsync(template, ct).AsTask();

    public Task RemoveAsync(MessageTemplate template, CancellationToken ct)
    {
        _db.MessageTemplates.Remove(template);
        return Task.CompletedTask;
    }
}

public sealed class AuditEventRepository : IAuditEventRepository
{
    private readonly RepairShopDbContext _db;
    public AuditEventRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(AuditEvent evt, CancellationToken ct)
        => _db.AuditEvents.AddAsync(evt, ct).AsTask();

    public Task<List<AuditEvent>> ListByEntityAsync(Guid shopId, string entityType, Guid entityId, int skip, int take, CancellationToken ct)
        => _db.AuditEvents
            .Where(x => x.ShopId == shopId && x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public async Task<(List<AuditEvent> Items, int Total)> SearchAsync(Guid shopId, AuditSearchOptions o, CancellationToken ct)
    {
        var q = _db.AuditEvents.Where(x => x.ShopId == shopId);
        if (!string.IsNullOrWhiteSpace(o.EntityType)) q = q.Where(x => x.EntityType == o.EntityType);
        if (o.EntityId is not null) q = q.Where(x => x.EntityId == o.EntityId);
        if (!string.IsNullOrWhiteSpace(o.Action)) q = q.Where(x => EF.Functions.ILike(x.Action, Like.Contains(o.Action)));
        if (o.ActorUserId is not null) q = q.Where(x => x.ActorUserId == o.ActorUserId);
        if (o.DateFromUtc is not null) q = q.Where(x => x.CreatedAtUtc >= o.DateFromUtc);
        if (o.DateToUtc is not null) q = q.Where(x => x.CreatedAtUtc <= o.DateToUtc);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAtUtc).Skip(Math.Max(0, o.Skip)).Take(Math.Clamp(o.Take, 1, 200)).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class NotificationOutboxRepository : INotificationOutboxRepository
{
    private readonly RepairShopDbContext _db;
    public NotificationOutboxRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(NotificationOutboxItem item, CancellationToken ct)
        => _db.NotificationOutbox.AddAsync(item, ct).AsTask();

    public Task<NotificationOutboxItem?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.NotificationOutbox.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<bool> ExistsAsync(Guid shopId, string correlationKey, CancellationToken ct)
        => _db.NotificationOutbox.IgnoreQueryFilters().AnyAsync(x => x.ShopId == shopId && x.CorrelationKey == correlationKey, ct);

    public Task<List<NotificationOutboxItem>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct)
        => _db.NotificationOutbox
            .Where(x => x.ShopId == shopId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public async Task<(List<NotificationOutboxItem> Items, int Total)> SearchAsync(Guid shopId, OutboxStatus? status, string? relatedEntityType, Guid? relatedEntityId, int skip, int take, CancellationToken ct)
    {
        var q = _db.NotificationOutbox.Where(x => x.ShopId == shopId);
        if (status is not null) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(relatedEntityType)) q = q.Where(x => x.RelatedEntityType == relatedEntityType);
        if (relatedEntityId is not null) q = q.Where(x => x.RelatedEntityId == relatedEntityId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAtUtc).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        return (items, total);
    }
}
