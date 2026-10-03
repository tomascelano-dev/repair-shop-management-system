using Microsoft.Extensions.Logging;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Common;
using RepairShop.Domain.Currency;

namespace RepairShop.Application.Currency;

/// <summary>
/// Daily exchange rates (manual or fetched from a provider) and conversions for reports and dashboards.
/// Conversion uses the latest rate on or before the date, preferring manual > oficial > blue.
/// </summary>
public sealed class ExchangeRateService
{
    private static readonly string[] SourcePreference = { "manual", "oficial", "blue" };

    private readonly IExchangeRateRepository _rates;
    private readonly IExchangeRateProvider _provider;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(IExchangeRateRepository rates, IExchangeRateProvider provider, IUnitOfWork uow, IDateTimeProvider clock, ILogger<ExchangeRateService> logger)
    {
        _rates = rates;
        _provider = provider;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public async Task<List<ExchangeRateResponse>> ListRecentAsync(int take, CancellationToken ct)
        => (await _rates.ListRecentAsync(take, ct)).Select(ToResponse).ToList();

    public async Task<ExchangeRateResponse> SetAsync(SetExchangeRateRequest req, CancellationToken ct)
    {
        var baseCur = Money.NormalizeCurrency(req.BaseCurrency);
        var quoteCur = Money.NormalizeCurrency(req.QuoteCurrency);
        var source = ExchangeRate.NormalizeSource(req.Source);
        var existing = await _rates.GetAsync(req.Date, baseCur, quoteCur, source, ct);
        if (existing is null)
        {
            existing = new ExchangeRate(req.Date, baseCur, quoteCur, source, req.Rate, _clock.UtcNow);
            await _rates.AddAsync(existing, ct);
        }
        else
        {
            existing.UpdateRate(req.Rate, _clock.UtcNow);
        }

        await _uow.SaveChangesAsync(ct);
        return ToResponse(existing);
    }

    /// <summary>Background job: stores today's rates from the configured provider (idempotent per day/source).</summary>
    public async Task<int> RefreshFromProviderAsync(CancellationToken ct)
    {
        IReadOnlyList<FetchedRate> fetched;
        try
        {
            fetched = await _provider.FetchAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exchange rate provider failed.");
            return 0;
        }

        foreach (var r in fetched)
            await SetAsync(new SetExchangeRateRequest(r.Date, r.Rate, r.BaseCurrency, r.QuoteCurrency, r.Source), ct);
        return fetched.Count;
    }

    public CurrencyConverter CreateConverter(string targetCurrency) => new(this, Money.NormalizeCurrency(targetCurrency));

    internal async Task<(decimal Rate, string Source)?> FindRateAsync(DateOnly date, string from, string to, CancellationToken ct)
    {
        foreach (var source in SourcePreference)
        {
            var direct = await _rates.GetLatestOnOrBeforeAsync(date, from, to, source, ct);
            if (direct is not null) return (direct.Rate, direct.Source);
            var inverse = await _rates.GetLatestOnOrBeforeAsync(date, to, from, source, ct);
            if (inverse is not null) return (1m / inverse.Rate, inverse.Source);
        }

        // Any source as a last resort.
        var any = await _rates.GetLatestOnOrBeforeAsync(date, from, to, null, ct);
        if (any is not null) return (any.Rate, any.Source);
        var anyInverse = await _rates.GetLatestOnOrBeforeAsync(date, to, from, null, ct);
        return anyInverse is null ? null : (1m / anyInverse.Rate, anyInverse.Source);
    }

    private static ExchangeRateResponse ToResponse(ExchangeRate r)
        => new(r.Id, r.Date, r.BaseCurrency, r.QuoteCurrency, r.Source, r.Rate, r.UpdatedAtUtc);
}

/// <summary>Converts amounts to a target currency caching rates per date (one converter per request).</summary>
public sealed class CurrencyConverter
{
    private readonly ExchangeRateService _service;
    private readonly Dictionary<(DateOnly, string), (decimal Rate, string Source)?> _cache = new();

    internal CurrencyConverter(ExchangeRateService service, string target)
    {
        _service = service;
        Target = target;
    }

    public string Target { get; }
    public HashSet<string> SourcesUsed { get; } = new();
    public HashSet<string> MissingCurrencies { get; } = new();

    public async Task<decimal?> ConvertAsync(decimal amount, string currency, DateTime atUtc, CancellationToken ct)
    {
        if (string.Equals(currency, Target, StringComparison.OrdinalIgnoreCase)) return amount;

        var date = DateOnly.FromDateTime(atUtc);
        if (!_cache.TryGetValue((date, currency), out var rate))
        {
            rate = await _service.FindRateAsync(date, currency, Target, ct);
            _cache[(date, currency)] = rate;
        }

        if (rate is null)
        {
            MissingCurrencies.Add(currency);
            return null;
        }

        SourcesUsed.Add(rate.Value.Source);
        return Money.Round(amount * rate.Value.Rate);
    }
}
