using RepairShop.Domain.Cash;
using RepairShop.Domain.Fiscal;
using RepairShop.Domain.Payments;
using RepairShop.Domain.Sales;

namespace RepairShop.Application.Abstractions;

public interface ISaleRepository
{
    Task<Sale?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<(List<Sale> Items, int Total)> SearchAsync(Guid shopId, SaleSearchOptions options, CancellationToken ct);
    Task<int> CountByCustomerAsync(Guid shopId, Guid customerId, CancellationToken ct);
    Task AddAsync(Sale sale, CancellationToken ct);
}

public sealed record SaleSearchOptions(
    string? Q = null,
    SaleStatus? Status = null,
    Guid? CustomerId = null,
    Guid? CashSessionId = null,
    DateTime? DateFromUtc = null,
    DateTime? DateToUtc = null,
    int Skip = 0,
    int Take = 50);

public interface ICashSessionRepository
{
    Task<CashRegisterSession?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<CashRegisterSession?> GetOpenAsync(Guid shopId, CancellationToken ct);
    Task<(List<CashRegisterSession> Items, int Total)> SearchAsync(Guid shopId, int skip, int take, CancellationToken ct);
    Task AddAsync(CashRegisterSession session, CancellationToken ct);
}

public interface ICashMovementRepository
{
    Task<List<CashMovement>> ListBySessionAsync(Guid shopId, Guid sessionId, CancellationToken ct);
    Task AddAsync(CashMovement movement, CancellationToken ct);
}

public interface IPaymentLinkRepository
{
    Task<PaymentLink?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<PaymentLink>> ListByEntityAsync(Guid shopId, string entityType, Guid entityId, CancellationToken ct);
    Task AddAsync(PaymentLink link, CancellationToken ct);
}

public interface IFiscalInvoiceRepository
{
    Task<FiscalInvoice?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<List<FiscalInvoice>> ListBySourceAsync(Guid shopId, string sourceType, Guid sourceId, CancellationToken ct);
    Task<(List<FiscalInvoice> Items, int Total)> SearchAsync(Guid shopId, DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken ct);
    Task AddAsync(FiscalInvoice invoice, CancellationToken ct);
}
