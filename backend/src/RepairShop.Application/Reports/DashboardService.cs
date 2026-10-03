using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Currency;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Common;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Shops;

namespace RepairShop.Application.Reports;

public sealed class DashboardService
{
    private readonly IReportQueries _queries;
    private readonly IShopRepository _shops;
    private readonly ExchangeRateService _rates;
    private readonly IDateTimeProvider _clock;

    public DashboardService(IReportQueries queries, IShopRepository shops, ExchangeRateService rates, IDateTimeProvider clock)
    {
        _queries = queries;
        _shops = shops;
        _rates = rates;
        _clock = clock;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(Guid shopId, Guid userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var (todayStart, monthStart) = LocalBoundaries(shop, now);

        var counts = await _queries.CountOrdersByStatusAsync(shopId, ct);
        int Count(RepairOrderStatus s) => counts.GetValueOrDefault(s);
        var total = counts.Values.Sum();
        var open = total - Count(RepairOrderStatus.Delivered) - Count(RepairOrderStatus.Cancelled);

        var converter = _rates.CreateConverter(shop.ReportingCurrency);
        var monthMoves = await _queries.MoneyMovementsAsync(shopId, monthStart, now, ct);
        var today = await SummarizeAsync(monthMoves.Where(m => m.AtUtc >= todayStart).ToList(), converter, ct);
        var month = await SummarizeAsync(monthMoves, converter, ct);

        // Legacy fields: all-time payments of repair orders (single currency only).
        var paymentsByCurrency = (await _queries.OrderPaymentsByCurrencyAsync(shopId, ct)).ToList();

        return new DashboardSummaryResponse(
            shopId, total, open, Count(RepairOrderStatus.Ready), Count(RepairOrderStatus.Delivered), Count(RepairOrderStatus.Cancelled),
            paymentsByCurrency.Count == 1 ? paymentsByCurrency[0].Amount : 0m,
            paymentsByCurrency.Count == 1 ? paymentsByCurrency[0].Currency : null,
            now,
            Enum.GetValues<RepairOrderStatus>().ToDictionary(s => s.ToString(), Count),
            await _queries.CountOverdueAsync(shopId, now, ct),
            await _queries.CountStaleAsync(shopId, now.AddDays(-shop.StaleOrderDays), ct),
            await _queries.CountReadyBeforeAsync(shopId, now.AddDays(-(shop.GetReadyReminderDays().FirstOrDefault() is var d and > 0 ? d : 7)), ct),
            await _queries.CountQuotesAsync(shopId, QuoteStatus.Sent, ct),
            await _queries.CountLowStockAsync(shopId, ct),
            await _queries.CountOpenAssignedAsync(shopId, userId, ct),
            today,
            month,
            (await _queries.TechnicianWorkloadAsync(shopId, now, ct)).Select(w => new TechnicianWorkload(w.UserId, w.Name, w.OpenOrders, w.OverdueOrders)).ToList(),
            await _queries.HasOpenCashSessionAsync(shopId, ct),
            shop.ReportingCurrency,
            converter.MissingCurrencies.ToList(),
            paymentsByCurrency);
    }

    public async Task<List<RevenuePoint>> GetRevenueSeriesAsync(Guid shopId, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 7, 366);
        var now = _clock.UtcNow;
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var tz = RenderOrderMessageService.ResolveTimeZone(shop.TimeZone);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, tz));
        var firstDay = localToday.AddDays(-(days - 1));
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(firstDay.ToDateTime(TimeOnly.MinValue), tz);

        var moves = await _queries.MoneyMovementsAsync(shopId, fromUtc, now, ct);
        var converter = _rates.CreateConverter(shop.ReportingCurrency);
        var byDay = moves.GroupBy(m => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(m.AtUtc, tz))).ToDictionary(g => g.Key, g => g.ToList());

        var points = new List<RevenuePoint>();
        for (var day = firstDay; day <= localToday; day = day.AddDays(1))
        {
            var list = byDay.GetValueOrDefault(day) ?? new List<MoneyMovementRow>();
            var summary = await SummarizeAsync(list, converter, ct);
            points.Add(new RevenuePoint(day, summary.Converted, summary.ByCurrency));
        }

        return points;
    }

    public async Task<ConsolidatedDashboardResponse> GetConsolidatedAsync(IReadOnlyList<(Guid ShopId, string Name)> shops, string reportingCurrency, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var converter = _rates.CreateConverter(reportingCurrency);
        var rows = new List<ConsolidatedBranchSummary>();
        decimal? total = 0;
        foreach (var (shopId, name) in shops)
        {
            var shop = await _shops.GetByIdAsync(shopId, ct);
            if (shop is null) continue;
            var (_, monthStart) = LocalBoundaries(shop, now);
            var counts = await _queries.CountOrdersByStatusAsync(shopId, ct);
            var open = counts.Where(c => c.Key is not (RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)).Sum(c => c.Value);
            var month = await SummarizeAsync(await _queries.MoneyMovementsAsync(shopId, monthStart, now, ct), converter, ct);
            total = total is null || month.Converted is null ? null : total + month.Converted;
            rows.Add(new ConsolidatedBranchSummary(shopId, name, open, counts.GetValueOrDefault(RepairOrderStatus.Ready),
                await _queries.CountOverdueAsync(shopId, now, ct), await _queries.CountLowStockAsync(shopId, ct), month));
        }

        return new ConsolidatedDashboardResponse(converter.Target, rows, total, converter.MissingCurrencies.ToList());
    }

    public static async Task<RevenueSummary> SummarizeAsync(IReadOnlyCollection<MoneyMovementRow> moves, CurrencyConverter converter, CancellationToken ct)
    {
        var byCurrency = moves.GroupBy(m => m.Currency).Select(g => new CurrencyAmount(g.Key, Money.Round(g.Sum(x => x.SignedAmount)))).OrderBy(x => x.Currency).ToList();
        decimal? converted = 0m;
        foreach (var m in moves)
        {
            var value = await converter.ConvertAsync(m.SignedAmount, m.Currency, m.AtUtc, ct);
            converted = value is null || converted is null ? null : converted + value;
        }

        return new RevenueSummary(byCurrency, converted is null ? null : Money.Round(converted.Value), converter.Target);
    }

    public static (DateTime TodayStartUtc, DateTime MonthStartUtc) LocalBoundaries(Shop shop, DateTime nowUtc)
    {
        var tz = RenderOrderMessageService.ResolveTimeZone(shop.TimeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var todayStart = TimeZoneInfo.ConvertTimeToUtc(local.Date, tz);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year, local.Month, 1), tz);
        return (todayStart, monthStart);
    }
}
