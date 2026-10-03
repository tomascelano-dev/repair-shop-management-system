using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class RepairOrderRepository : IRepairOrderRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderRepository(RepairShopDbContext db) => _db = db;

    public Task<RepairOrder?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.RepairOrders.FirstOrDefaultAsync(x => x.Id == id && x.ShopId == shopId, ct);

    public Task<RepairOrder?> GetByNumberAsync(Guid shopId, int orderNumber, CancellationToken ct)
        => _db.RepairOrders.FirstOrDefaultAsync(x => x.ShopId == shopId && x.OrderNumber == orderNumber, ct);

    public Task<RepairOrder?> GetByPublicTokenAsync(string token, CancellationToken ct)
        => _db.RepairOrders.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.PublicToken == token, ct);

    public Task<List<RepairOrder>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct)
        => _db.RepairOrders
            .Where(x => x.ShopId == shopId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public async Task<(List<RepairOrder> Items, int Total)> SearchAsync(Guid shopId, RepairOrderSearchOptions options, CancellationToken ct)
    {
        var q = Filter(_db, shopId, options);
        q = ApplySort(q, options.SortBy, options.SortDir);

        var total = await q.CountAsync(ct);
        var take = Math.Clamp(options.Take, 1, 200);
        var skip = Math.Max(0, options.Skip);

        var items = await q.Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    internal static IQueryable<RepairOrder> Filter(RepairShopDbContext db, Guid shopId, RepairOrderSearchOptions options)
    {
        var q = db.RepairOrders.AsQueryable().Where(x => x.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(options.Q))
        {
            var term = options.Q.Trim();
            var pattern = Like.Contains(term);
            var numberText = term.TrimStart('#').TrimStart('0');
            var hasNumber = int.TryParse(numberText.Length == 0 ? "0" : numberText, out var number) && number > 0;

            q = q.Where(x =>
                (hasNumber && x.OrderNumber == number)
                || EF.Functions.ILike(x.IssueDescription, pattern)
                || (x.Notes != null && EF.Functions.ILike(x.Notes, pattern))
                || (x.IssueCategory != null && EF.Functions.ILike(x.IssueCategory, pattern))
                || db.Customers.Any(c => c.Id == x.CustomerId && (EF.Functions.ILike(c.FullName, pattern) || EF.Functions.ILike(c.Phone, pattern)))
                || db.Devices.Any(d => d.Id == x.DeviceId && (
                    EF.Functions.ILike(d.Brand, pattern)
                    || EF.Functions.ILike(d.Model, pattern)
                    || (d.Label != null && EF.Functions.ILike(d.Label, pattern))
                    || (d.SerialNumber != null && EF.Functions.ILike(d.SerialNumber, pattern))
                    || (d.Imei != null && EF.Functions.ILike(d.Imei, pattern)))));
        }

        if (options.Status is not null) q = q.Where(x => x.Status == options.Status);
        if (options.Statuses is { Count: > 0 }) q = q.Where(x => options.Statuses.Contains(x.Status));
        if (options.OnlyOpen == true) q = q.Where(x => x.Status != RepairOrderStatus.Delivered && x.Status != RepairOrderStatus.Cancelled);
        if (options.OnlyOverdue == true)
        {
            var now = options.NowUtc ?? DateTime.UtcNow;
            q = q.Where(x => x.PromisedAtUtc != null && x.PromisedAtUtc < now
                             && x.Status != RepairOrderStatus.Delivered && x.Status != RepairOrderStatus.Cancelled && x.Status != RepairOrderStatus.Ready);
        }
        if (options.Priority is not null) q = q.Where(x => x.Priority == options.Priority);
        if (options.AssignedTechnicianId is not null)
        {
            q = options.AssignedTechnicianId == Guid.Empty
                ? q.Where(x => x.AssignedTechnicianId == null)
                : q.Where(x => x.AssignedTechnicianId == options.AssignedTechnicianId);
        }
        if (options.CustomerId is not null) q = q.Where(x => x.CustomerId == options.CustomerId);
        if (options.DeviceId is not null) q = q.Where(x => x.DeviceId == options.DeviceId);
        if (options.DateFromUtc is not null) q = q.Where(x => x.CreatedAtUtc >= options.DateFromUtc);
        if (options.DateToUtc is not null) q = q.Where(x => x.CreatedAtUtc <= options.DateToUtc);
        return q;
    }

    internal static IQueryable<RepairOrder> ApplySort(IQueryable<RepairOrder> q, string? sortBy, string? sortDir)
    {
        sortBy = (sortBy ?? "createdAt").Trim();
        sortDir = (sortDir ?? "desc").Trim();
        var desc = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy.ToLowerInvariant(), desc) switch
        {
            ("createdat", true) => q.OrderByDescending(x => x.CreatedAtUtc),
            ("createdat", false) => q.OrderBy(x => x.CreatedAtUtc),
            ("updatedat", true) => q.OrderByDescending(x => x.UpdatedAtUtc),
            ("updatedat", false) => q.OrderBy(x => x.UpdatedAtUtc),
            ("status", true) => q.OrderByDescending(x => x.Status),
            ("status", false) => q.OrderBy(x => x.Status),
            ("number", true) => q.OrderByDescending(x => x.OrderNumber),
            ("number", false) => q.OrderBy(x => x.OrderNumber),
            ("promisedat", true) => q.OrderByDescending(x => x.PromisedAtUtc),
            ("promisedat", false) => q.OrderBy(x => x.PromisedAtUtc == null).ThenBy(x => x.PromisedAtUtc),
            ("priority", true) => q.OrderByDescending(x => x.Priority).ThenBy(x => x.CreatedAtUtc),
            ("priority", false) => q.OrderBy(x => x.Priority).ThenBy(x => x.CreatedAtUtc),
            _ => q.OrderByDescending(x => x.CreatedAtUtc)
        };
    }

    public Task<List<RepairOrder>> ListForJobsAsync(RepairOrderStatus status, DateTime? readyBeforeUtc, DateTime? deliveredAfterUtc, DateTime? deliveredBeforeUtc, int take, CancellationToken ct)
    {
        var q = _db.RepairOrders.IgnoreQueryFilters().Where(x => x.Status == status);
        if (readyBeforeUtc is not null) q = q.Where(x => x.ReadyAtUtc != null && x.ReadyAtUtc <= readyBeforeUtc);
        if (deliveredAfterUtc is not null) q = q.Where(x => x.DeliveredAtUtc != null && x.DeliveredAtUtc >= deliveredAfterUtc);
        if (deliveredBeforeUtc is not null) q = q.Where(x => x.DeliveredAtUtc != null && x.DeliveredAtUtc <= deliveredBeforeUtc);
        return q.OrderBy(x => x.UpdatedAtUtc).Take(take).ToListAsync(ct);
    }

    public Task<bool> HasWarrantyClaimsAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrders.AnyAsync(x => x.ShopId == shopId && x.WarrantyOfOrderId == orderId, ct);

    public Task AddAsync(RepairOrder order, CancellationToken ct)
        => _db.RepairOrders.AddAsync(order, ct).AsTask();

    public Task RemoveAsync(RepairOrder order, CancellationToken ct)
    {
        _db.RepairOrders.Remove(order);
        return Task.CompletedTask;
    }
}

public sealed class RepairOrderStatusHistoryRepository : IRepairOrderStatusHistoryRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderStatusHistoryRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(RepairOrderStatusHistory history, CancellationToken ct)
        => _db.RepairOrderStatusHistory.AddAsync(history, ct).AsTask();

    public Task<List<RepairOrderStatusHistory>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderStatusHistory
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.ChangedAtUtc)
            .ToListAsync(ct);
}

public sealed class RepairOrderNoteRepository : IRepairOrderNoteRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderNoteRepository(RepairShopDbContext db) => _db = db;

    public Task<List<RepairOrderNote>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderNotes
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<List<RepairOrderNote>> ListPublicByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderNotes
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId && x.IsPublic)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

    public Task AddAsync(RepairOrderNote note, CancellationToken ct)
        => _db.RepairOrderNotes.AddAsync(note, ct).AsTask();
}

public sealed class RepairOrderAttachmentRepository : IRepairOrderAttachmentRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderAttachmentRepository(RepairShopDbContext db) => _db = db;

    public Task<RepairOrderAttachment?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.RepairOrderAttachments.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<List<RepairOrderAttachment>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderAttachments
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

    public Task AddAsync(RepairOrderAttachment attachment, CancellationToken ct)
        => _db.RepairOrderAttachments.AddAsync(attachment, ct).AsTask();

    public void Remove(RepairOrderAttachment attachment) => _db.RepairOrderAttachments.Remove(attachment);
}

public sealed class RepairOrderPaymentRepository : IRepairOrderPaymentRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderPaymentRepository(RepairShopDbContext db) => _db = db;

    public Task<RepairOrderPayment?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.RepairOrderPayments.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<List<RepairOrderPayment>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderPayments
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<decimal> SumByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        var rows = await _db.RepairOrderPayments
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .GroupBy(x => x.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var paid = rows.Where(r => r.Type == PaymentType.Payment).Sum(r => r.Total);
        var refunded = rows.Where(r => r.Type == PaymentType.Refund).Sum(r => r.Total);
        return Money.Round(paid - refunded);
    }

    public Task<bool> ExistsExternalAsync(Guid shopId, string externalPaymentId, CancellationToken ct)
        => _db.RepairOrderPayments.IgnoreQueryFilters().AnyAsync(x => x.ShopId == shopId && x.ExternalPaymentId == externalPaymentId, ct);

    public async Task<decimal> SumRefundsAsync(Guid shopId, Guid paymentId, CancellationToken ct)
        => await _db.RepairOrderPayments
            .Where(x => x.ShopId == shopId && x.RefundOfPaymentId == paymentId && x.Type == PaymentType.Refund)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

    public Task AddAsync(RepairOrderPayment payment, CancellationToken ct)
        => _db.RepairOrderPayments.AddAsync(payment, ct).AsTask();
}

public sealed class RepairOrderReceptionChecklistRepository : IRepairOrderReceptionChecklistRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderReceptionChecklistRepository(RepairShopDbContext db) => _db = db;

    public Task<RepairOrderReceptionChecklist?> GetByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderReceptionChecklists.FirstOrDefaultAsync(x => x.ShopId == shopId && x.RepairOrderId == orderId, ct);

    public Task AddAsync(RepairOrderReceptionChecklist checklist, CancellationToken ct)
        => _db.RepairOrderReceptionChecklists.AddAsync(checklist, ct).AsTask();
}

public sealed class RepairOrderQaChecklistRepository : IRepairOrderQaChecklistRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderQaChecklistRepository(RepairShopDbContext db) => _db = db;

    public Task<RepairOrderQaChecklist?> GetByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderQaChecklists.FirstOrDefaultAsync(x => x.ShopId == shopId && x.RepairOrderId == orderId, ct);

    public Task AddAsync(RepairOrderQaChecklist checklist, CancellationToken ct)
        => _db.RepairOrderQaChecklists.AddAsync(checklist, ct).AsTask();
}

public sealed class RepairOrderPartUsageRepository : IRepairOrderPartUsageRepository
{
    private readonly RepairShopDbContext _db;
    public RepairOrderPartUsageRepository(RepairShopDbContext db) => _db = db;

    public Task AddAsync(RepairOrderPartUsage usage, CancellationToken ct)
        => _db.RepairOrderPartUsages.AddAsync(usage, ct).AsTask();

    public Task<List<RepairOrderPartUsage>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderPartUsages
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<int> CountByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.RepairOrderPartUsages.CountAsync(x => x.ShopId == shopId && x.RepairOrderId == orderId, ct);
}

public sealed class QuoteRepository : IQuoteRepository
{
    private readonly RepairShopDbContext _db;
    public QuoteRepository(RepairShopDbContext db) => _db = db;

    public Task<Quote?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.Quotes.Include(x => x.Items).FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<List<Quote>> ListByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.Quotes.Include(x => x.Items)
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(ct);

    public Task<Quote?> GetApprovedAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => _db.Quotes.Include(x => x.Items)
            .Where(x => x.ShopId == shopId && x.RepairOrderId == orderId && x.Status == QuoteStatus.Approved)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);

    public async Task<int> GetMaxVersionAsync(Guid shopId, Guid orderId, CancellationToken ct)
        => await _db.Quotes.Where(x => x.ShopId == shopId && x.RepairOrderId == orderId).MaxAsync(x => (int?)x.Version, ct) ?? 0;

    public Task<List<Quote>> ListExpiredSentAsync(DateTime nowUtc, int take, CancellationToken ct)
        => _db.Quotes.IgnoreQueryFilters().Include(x => x.Items)
            .Where(x => x.Status == QuoteStatus.Sent && x.ValidUntilUtc != null && x.ValidUntilUtc < nowUtc)
            .OrderBy(x => x.ValidUntilUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task AddAsync(Quote quote, CancellationToken ct)
        => _db.Quotes.AddAsync(quote, ct).AsTask();
}
