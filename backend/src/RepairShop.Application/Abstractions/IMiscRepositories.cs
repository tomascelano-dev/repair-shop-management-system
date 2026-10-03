using RepairShop.Domain.Currency;
using RepairShop.Domain.Feedback;
using RepairShop.Domain.Files;

namespace RepairShop.Application.Abstractions;

public interface IStoredFileRepository
{
    Task<StoredFile?> GetByIdAsync(Guid shopId, Guid id, CancellationToken ct);
    Task<StoredFile?> GetByIdAnyShopAsync(Guid id, CancellationToken ct);
    Task AddAsync(StoredFile file, CancellationToken ct);
}

public interface IExchangeRateRepository
{
    Task<ExchangeRate?> GetAsync(DateOnly date, string baseCurrency, string quoteCurrency, string source, CancellationToken ct);

    /// <summary>Latest rate on or before the date (any source if null).</summary>
    Task<ExchangeRate?> GetLatestOnOrBeforeAsync(DateOnly date, string baseCurrency, string quoteCurrency, string? source, CancellationToken ct);

    Task<List<ExchangeRate>> ListRecentAsync(int take, CancellationToken ct);
    Task<List<ExchangeRate>> ListRangeAsync(DateOnly from, DateOnly to, string baseCurrency, string quoteCurrency, CancellationToken ct);
    Task AddAsync(ExchangeRate rate, CancellationToken ct);
}

public interface ICustomerFeedbackRepository
{
    Task<CustomerFeedback?> GetByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task AddAsync(CustomerFeedback feedback, CancellationToken ct);
}
