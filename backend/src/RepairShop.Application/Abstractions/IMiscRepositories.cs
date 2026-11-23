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
    /// <summary>The row of exactly that scope: <paramref name="organizationId"/> null = global (platform) rate.</summary>
    Task<ExchangeRate?> GetAsync(Guid? organizationId, DateOnly date, string baseCurrency, string quoteCurrency, string source, CancellationToken ct);

    /// <summary>Latest rate of exactly that scope on or before the date (any source if null).</summary>
    Task<ExchangeRate?> GetLatestOnOrBeforeAsync(Guid? organizationId, DateOnly date, string baseCurrency, string quoteCurrency, string? source, CancellationToken ct);

    /// <summary>Global rates plus the organization's own.</summary>
    Task<List<ExchangeRate>> ListRecentAsync(Guid organizationId, int take, CancellationToken ct);
    Task<List<ExchangeRate>> ListRangeAsync(DateOnly from, DateOnly to, string baseCurrency, string quoteCurrency, CancellationToken ct);
    Task AddAsync(ExchangeRate rate, CancellationToken ct);
}

public interface ICustomerFeedbackRepository
{
    Task<CustomerFeedback?> GetByOrderAsync(Guid shopId, Guid orderId, CancellationToken ct);
    Task AddAsync(CustomerFeedback feedback, CancellationToken ct);
}
