using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Cash;
using RepairShop.Domain.Fiscal;
using RepairShop.Domain.Payments;
using RepairShop.Domain.Sales;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Infrastructure.Repositories;

public sealed class SaleRepository : ISaleRepository
{
    private readonly RepairShopDbContext _db;
    public SaleRepository(RepairShopDbContext db) => _db = db;

    private IQueryable<Sale> WithChildren()
        => _db.Sales
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .Include(x => x.Refunds).ThenInclude(r => r.Lines)
            .AsSplitQuery();

    public Task<Sale?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => WithChildren().FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public async Task<(List<Sale> Items, int Total)> SearchAsync(Guid shopId, SaleSearchOptions o, CancellationToken ct)
    {
        var q = WithChildren().Where(x => x.ShopId == shopId);
        if (o.Status is not null) q = q.Where(x => x.Status == o.Status);
        if (o.CustomerId is not null) q = q.Where(x => x.CustomerId == o.CustomerId);
        if (o.CashSessionId is not null) q = q.Where(x => x.CashSessionId == o.CashSessionId);
        if (o.DateFromUtc is not null) q = q.Where(x => x.CreatedAtUtc >= o.DateFromUtc);
        if (o.DateToUtc is not null) q = q.Where(x => x.CreatedAtUtc <= o.DateToUtc);
        if (!string.IsNullOrWhiteSpace(o.Q))
        {
            var term = o.Q.Trim();
            var numberText = term.TrimStart('V', 'v', '-', '#').TrimStart('0');
            var hasNumber = int.TryParse(numberText, out var number) && number > 0;
            var p = Like.Contains(term);
            q = q.Where(x => (hasNumber && x.Number == number)
                             || x.Lines.Any(l => EF.Functions.ILike(l.Description, p) || EF.Functions.ILike(l.Sku, p))
                             || (x.CustomerId != null && _db.Customers.Any(c => c.Id == x.CustomerId && EF.Functions.ILike(c.FullName, p))));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.Number).Skip(Math.Max(0, o.Skip)).Take(Math.Clamp(o.Take, 1, 200)).ToListAsync(ct);
        return (items, total);
    }

    public Task<int> CountByCustomerAsync(Guid shopId, Guid customerId, CancellationToken ct)
        => _db.Sales.CountAsync(x => x.ShopId == shopId && x.CustomerId == customerId, ct);

    public Task AddAsync(Sale sale, CancellationToken ct)
        => _db.Sales.AddAsync(sale, ct).AsTask();
}

public sealed class CashSessionRepository : ICashSessionRepository
{
    private readonly RepairShopDbContext _db;
    public CashSessionRepository(RepairShopDbContext db) => _db = db;

    public Task<CashRegisterSession?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.CashSessions.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<CashRegisterSession?> GetOpenAsync(Guid shopId, CancellationToken ct)
        => _db.CashSessions.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Status == CashSessionStatus.Open, ct);

    public async Task<(List<CashRegisterSession> Items, int Total)> SearchAsync(Guid shopId, int skip, int take, CancellationToken ct)
    {
        var q = _db.CashSessions.Where(x => x.ShopId == shopId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.Number).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        return (items, total);
    }

    public Task AddAsync(CashRegisterSession session, CancellationToken ct)
        => _db.CashSessions.AddAsync(session, ct).AsTask();
}

public sealed class CashMovementRepository : ICashMovementRepository
{
    private readonly RepairShopDbContext _db;
    public CashMovementRepository(RepairShopDbContext db) => _db = db;

    public Task<List<CashMovement>> ListBySessionAsync(Guid shopId, Guid sessionId, CancellationToken ct)
        => _db.CashMovements.Where(x => x.ShopId == shopId && x.SessionId == sessionId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);

    public Task AddAsync(CashMovement movement, CancellationToken ct)
        => _db.CashMovements.AddAsync(movement, ct).AsTask();
}

public sealed class PaymentLinkRepository : IPaymentLinkRepository
{
    private readonly RepairShopDbContext _db;
    public PaymentLinkRepository(RepairShopDbContext db) => _db = db;

    public Task<PaymentLink?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.PaymentLinks.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<PaymentLink>> ListByEntityAsync(Guid shopId, string entityType, Guid entityId, CancellationToken ct)
        => _db.PaymentLinks.IgnoreQueryFilters()
            .Where(x => x.ShopId == shopId && x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

    public Task AddAsync(PaymentLink link, CancellationToken ct)
        => _db.PaymentLinks.AddAsync(link, ct).AsTask();
}

public sealed class FiscalInvoiceRepository : IFiscalInvoiceRepository
{
    private readonly RepairShopDbContext _db;
    public FiscalInvoiceRepository(RepairShopDbContext db) => _db = db;

    public Task<FiscalInvoice?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct)
        => _db.FiscalInvoices.FirstOrDefaultAsync(x => x.ShopId == shopId && x.Id == id, ct);

    public Task<List<FiscalInvoice>> ListBySourceAsync(Guid shopId, string sourceType, Guid sourceId, CancellationToken ct)
        => _db.FiscalInvoices.Where(x => x.ShopId == shopId && x.SourceType == sourceType && x.SourceId == sourceId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    public async Task<(List<FiscalInvoice> Items, int Total)> SearchAsync(Guid shopId, DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken ct)
    {
        var q = _db.FiscalInvoices.Where(x => x.ShopId == shopId);
        if (fromUtc is not null) q = q.Where(x => x.CreatedAtUtc >= fromUtc);
        if (toUtc is not null) q = q.Where(x => x.CreatedAtUtc <= toUtc);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAtUtc).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        return (items, total);
    }

    public Task AddAsync(FiscalInvoice invoice, CancellationToken ct)
        => _db.FiscalInvoices.AddAsync(invoice, ct).AsTask();
}
