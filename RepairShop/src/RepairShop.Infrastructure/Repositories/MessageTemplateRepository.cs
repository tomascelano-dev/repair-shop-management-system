using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Messaging;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

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
