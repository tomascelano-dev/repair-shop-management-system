using System.Globalization;
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

/// <summary>Business reports. Amounts are reported per currency and converted to the shop's reporting currency.</summary>
public sealed class ReportService
{
    private readonly IReportQueries _q;
    private readonly IShopRepository _shops;
    private readonly ExchangeRateService _rates;
    private readonly IExcelExporter _excel;
    private readonly IDateTimeProvider _clock;

    public ReportService(IReportQueries q, IShopRepository shops, ExchangeRateService rates, IExcelExporter excel, IDateTimeProvider clock)
    {
        _q = q;
        _shops = shops;
        _rates = rates;
        _excel = excel;
        _clock = clock;
    }

    public async Task<RevenueReport> RevenueAsync(Guid shopId, DateTime? from, DateTime? to, string groupBy, CancellationToken ct)
    {
        var (shop, f, t) = await RangeAsync(shopId, from, to, ct);
        var tz = RenderOrderMessageService.ResolveTimeZone(shop.TimeZone);
        var converter = _rates.CreateConverter(shop.ReportingCurrency);
        var moves = await _q.MoneyMovementsAsync(shopId, f, t, ct);

        string PeriodKey(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            return (groupBy ?? "day").ToLowerInvariant() switch
            {
                "month" => local.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                "week" => $"{ISOWeek.GetYear(local)}-S{ISOWeek.GetWeekOfYear(local):D2}",
                _ => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
        }

        var rows = new List<RevenueRow>();
        foreach (var g in moves.GroupBy(m => (Period: PeriodKey(m.AtUtc), m.Currency, m.Method)).OrderBy(g => g.Key.Period).ThenBy(g => g.Key.Currency).ThenBy(g => g.Key.Method))
        {
            var orders = g.Where(m => m.Kind == "order_payment").Sum(m => m.SignedAmount);
            var sales = g.Where(m => m.Kind == "sale").Sum(m => m.SignedAmount);
            var refunds = g.Where(m => m.Kind is "order_refund" or "sale_refund").Sum(m => m.SignedAmount);
            decimal? converted = 0;
            foreach (var m in g)
            {
                var v = await converter.ConvertAsync(m.SignedAmount, m.Currency, m.AtUtc, ct);
                converted = v is null || converted is null ? null : converted + v;
            }
            rows.Add(new RevenueRow(g.Key.Period, g.Key.Currency, g.Key.Method, Money.Round(orders), Money.Round(sales), Money.Round(refunds),
                Money.Round(orders + sales + refunds), converted is null ? null : Money.Round(converted.Value)));
        }

        var totals = moves.GroupBy(m => m.Currency).Select(g => new CurrencyAmount(g.Key, Money.Round(g.Sum(x => x.SignedAmount)))).ToList();
        var byMethod = moves.GroupBy(m => m.Method).Select(g => new CurrencyAmount(g.Key, Money.Round(g.Sum(x => x.SignedAmount)))).ToList();
        decimal? totalConverted = rows.Any(r => r.NetConverted is null) ? null : Money.Round(rows.Sum(r => r.NetConverted ?? 0));
        return new RevenueReport(Period(f, t, converter), rows, totals, totalConverted, byMethod);
    }

    public async Task<MarginReport> MarginsAsync(Guid shopId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (_, f, t) = await RangeAsync(shopId, from, to, ct);
        var shop = (await _shops.GetByIdAsync(shopId, ct))!;
        var converter = _rates.CreateConverter(shop.ReportingCurrency);

        var orders = (await _q.OrderFactsAsync(shopId, f, t, byDelivery: true, ct)).Where(o => o.Status == RepairOrderStatus.Delivered).ToList();
        var parts = await _q.PartUsesAsync(shopId, orders.Select(o => o.Id).ToList(), ct);
        var rows = new List<MarginRow>();

        foreach (var o in orders)
        {
            var currency = o.Currency ?? shop.DefaultCurrency;
            var revenue = Money.Round((o.AgreedPrice ?? 0) + o.ExtraCharges);
            var cost = Money.Round(parts.Where(p => p.OrderId == o.Id && p.UnitCost is not null && (p.CostCurrency ?? currency) == currency).Sum(p => p.UnitCost!.Value * p.Quantity));
            rows.Add(new MarginRow("order", o.Id, RepairOrder.FormatCode(o.OrderNumber), $"{o.Brand} {o.Model} · {o.CustomerName}", o.DeliveredAtUtc ?? o.CreatedAtUtc,
                currency, revenue, cost, Money.Round(revenue - cost), revenue == 0 ? null : Math.Round((double)((revenue - cost) / revenue) * 100, 1)));
        }

        foreach (var s in (await _q.SaleFactsAsync(shopId, f, t, ct)).Where(s => !s.Voided))
        {
            var revenue = Money.Round(s.Total - s.Refunded);
            rows.Add(new MarginRow("sale", s.Id, $"V-{s.Number:D6}", "Venta de mostrador", s.CreatedAtUtc, s.Currency, revenue, Money.Round(s.Cost),
                Money.Round(revenue - s.Cost), revenue == 0 ? null : Math.Round((double)((revenue - s.Cost) / revenue) * 100, 1)));
        }

        decimal? marginConverted = 0;
        foreach (var r in rows)
        {
            var v = await converter.ConvertAsync(r.Margin, r.Currency, r.DateUtc, ct);
            marginConverted = v is null || marginConverted is null ? null : marginConverted + v;
        }

        return new MarginReport(Period(f, t, converter), rows.OrderByDescending(r => r.DateUtc).ToList(),
            rows.GroupBy(r => r.Currency).Select(g => new CurrencyAmount(g.Key, Money.Round(g.Sum(x => x.Revenue)))).ToList(),
            rows.GroupBy(r => r.Currency).Select(g => new CurrencyAmount(g.Key, Money.Round(g.Sum(x => x.Margin)))).ToList(),
            marginConverted is null ? null : Money.Round(marginConverted.Value));
    }

    public async Task<RepairTimesReport> RepairTimesAsync(Guid shopId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (shop, f, t) = await RangeAsync(shopId, from, to, ct);
        var orders = await _q.OrderFactsAsync(shopId, f, t, byDelivery: false, ct);
        var changes = await _q.StatusChangesAsync(shopId, orders.Select(o => o.Id).ToList(), ct);
        var now = _clock.UtcNow;

        // Time spent in each status = time until the next change (or now if still there).
        var durations = new Dictionary<RepairOrderStatus, List<double>>();
        foreach (var o in orders)
        {
            var seq = changes.Where(c => c.OrderId == o.Id).OrderBy(c => c.AtUtc).ToList();
            var current = RepairOrderStatus.Received;
            var since = o.CreatedAtUtc;
            foreach (var c in seq)
            {
                Add(durations, current, (c.AtUtc - since).TotalHours);
                current = c.To;
                since = c.AtUtc;
            }
            if (current is not (RepairOrderStatus.Delivered or RepairOrderStatus.Cancelled)) Add(durations, current, (now - since).TotalHours);
        }

        var byStatus = durations.Select(kv => new StatusDurationRow(kv.Key.ToString(), OrderLabels.Status(kv.Key), kv.Value.Count,
                Math.Round(kv.Value.Average(), 1), Math.Round(Median(kv.Value), 1)))
            .OrderBy(r => Enum.Parse<RepairOrderStatus>(r.Status) switch
            {
                RepairOrderStatus.Received => 0, RepairOrderStatus.Diagnosing => 1, RepairOrderStatus.WaitingParts => 2, RepairOrderStatus.InProgress => 3,
                RepairOrderStatus.Testing => 4, RepairOrderStatus.Ready => 5, _ => 6
            }).ToList();

        var toReady = orders.Where(o => o.ReadyAtUtc is not null).Select(o => (o.ReadyAtUtc!.Value - o.CreatedAtUtc).TotalHours).ToList();
        var toDelivery = orders.Where(o => o.DeliveredAtUtc is not null).Select(o => (o.DeliveredAtUtc!.Value - o.CreatedAtUtc).TotalHours).ToList();
        var bottleneck = byStatus.Where(r => r.Status is not nameof(RepairOrderStatus.Ready)).OrderByDescending(r => r.AverageHours).FirstOrDefault()?.Label;

        return new RepairTimesReport(Period(f, t, _rates.CreateConverter(shop.ReportingCurrency)), orders.Count,
            toReady.Count == 0 ? null : Math.Round(toReady.Average(), 1), toDelivery.Count == 0 ? null : Math.Round(toDelivery.Average(), 1), byStatus, bottleneck);
    }

    public async Task<QuoteStatsReport> QuotesAsync(Guid shopId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (shop, f, t) = await RangeAsync(shopId, from, to, ct);
        var quotes = (await _q.QuoteFactsAsync(shopId, f, t, ct)).Where(q => q.Status != QuoteStatus.Superseded).ToList();
        int C(QuoteStatus s) => quotes.Count(q => q.Status == s);
        var decided = C(QuoteStatus.Approved) + C(QuoteStatus.Rejected) + C(QuoteStatus.Expired);
        return new QuoteStatsReport(Period(f, t, _rates.CreateConverter(shop.ReportingCurrency)), quotes.Count, C(QuoteStatus.Approved), C(QuoteStatus.Rejected),
            C(QuoteStatus.Expired), C(QuoteStatus.Draft) + C(QuoteStatus.Sent),
            decided == 0 ? null : Math.Round(100.0 * C(QuoteStatus.Approved) / decided, 1),
            quotes.Where(q => q.Status == QuoteStatus.Approved).GroupBy(q => q.Currency)
                .Select(g => new CurrencyAmount(g.Key, Money.Round(g.Average(x => x.Total)))).ToList());
    }

    public async Task<TopIssuesReport> TopIssuesAsync(Guid shopId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (shop, f, t) = await RangeAsync(shopId, from, to, ct);
        var orders = await _q.OrderFactsAsync(shopId, f, t, byDelivery: false, ct);

        List<TopRow> Top(Func<OrderFactRow, string> key) => orders.GroupBy(key)
            .Select(g =>
            {
                var priced = g.Where(o => o.AgreedPrice is > 0).ToList();
                var currency = priced.GroupBy(o => o.Currency).OrderByDescending(x => x.Count()).FirstOrDefault()?.Key;
                var avg = priced.Where(o => o.Currency == currency).Select(o => o.AgreedPrice!.Value + o.ExtraCharges).DefaultIfEmpty().Average();
                return new TopRow(g.Key, g.Count(), priced.Count == 0 ? null : Money.Round(avg), currency);
            })
            .OrderByDescending(r => r.Orders).Take(15).ToList();

        return new TopIssuesReport(Period(f, t, _rates.CreateConverter(shop.ReportingCurrency)),
            Top(o => string.IsNullOrWhiteSpace(o.Category) ? "Sin categoría" : o.Category!),
            Top(o => $"{o.Brand} {o.Model}"),
            Top(o => o.Brand));
    }

    public async Task<TechniciansReport> TechniciansAsync(Guid shopId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (shop, f, t) = await RangeAsync(shopId, from, to, ct);
        var orders = await _q.OrderFactsAsync(shopId, f, t, byDelivery: false, ct);
        var all = await _q.OrderFactsAsync(shopId, DateTime.UnixEpoch, _clock.UtcNow, byDelivery: false, ct);
        var claims = all.Where(o => o.IsWarrantyClaim && o.WarrantyOfOrderId is not null).ToList();
        var byId = all.ToDictionary(o => o.Id);

        var rows = orders.Where(o => o.TechnicianId is not null).GroupBy(o => (o.TechnicianId!.Value, o.TechnicianName ?? "?"))
            .Select(g =>
            {
                var delivered = g.Where(o => o.Status == RepairOrderStatus.Delivered && !o.IsWarrantyClaim).ToList();
                var ready = g.Where(o => o.ReadyAtUtc is not null).Select(o => (o.ReadyAtUtc!.Value - o.CreatedAtUtc).TotalHours).ToList();
                var techClaims = claims.Count(c => byId.TryGetValue(c.WarrantyOfOrderId!.Value, out var orig) && orig.TechnicianId == g.Key.Item1
                                                   && delivered.Any(d => d.Id == orig.Id));
                return new TechnicianRow(g.Key.Item1, g.Key.Item2, g.Count(), delivered.Count,
                    ready.Count == 0 ? null : Math.Round(ready.Average(), 1),
                    delivered.GroupBy(o => o.Currency ?? shop.DefaultCurrency).Select(x => new CurrencyAmount(x.Key, Money.Round(x.Sum(o => (o.AgreedPrice ?? 0) + o.ExtraCharges)))).ToList(),
                    techClaims, delivered.Count == 0 ? null : Math.Round(100.0 * techClaims / delivered.Count, 1));
            })
            .OrderByDescending(r => r.Delivered).ToList();

        return new TechniciansReport(Period(f, t, _rates.CreateConverter(shop.ReportingCurrency)), rows);
    }

    public async Task<WarrantyReport> WarrantyAsync(Guid shopId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (shop, f, t) = await RangeAsync(shopId, from, to, ct);
        var delivered = (await _q.OrderFactsAsync(shopId, f, t, byDelivery: true, ct)).Where(o => o.Status == RepairOrderStatus.Delivered && !o.IsWarrantyClaim).ToList();
        var all = await _q.OrderFactsAsync(shopId, f, _clock.UtcNow, byDelivery: false, ct);
        var deliveredIds = delivered.Select(o => o.Id).ToHashSet();
        var claimsOf = all.Where(o => o.IsWarrantyClaim && o.WarrantyOfOrderId is not null && deliveredIds.Contains(o.WarrantyOfOrderId.Value))
            .Select(o => o.WarrantyOfOrderId!.Value).ToHashSet();

        var parts = await _q.PartUsesAsync(shopId, deliveredIds.ToList(), ct);
        var byPart = parts.GroupBy(p => (p.ItemId, p.Sku, p.Name))
            .Select(g =>
            {
                var orders = g.Select(p => p.OrderId).Distinct().ToList();
                var claims = orders.Count(claimsOf.Contains);
                return new WarrantyPartRow(g.Key.ItemId, g.Key.Sku, g.Key.Name, orders.Count, claims, orders.Count == 0 ? null : Math.Round(100.0 * claims / orders.Count, 1));
            })
            .OrderByDescending(r => r.Claims).ThenByDescending(r => r.Uses).ToList();

        return new WarrantyReport(Period(f, t, _rates.CreateConverter(shop.ReportingCurrency)), delivered.Count, claimsOf.Count,
            delivered.Count == 0 ? null : Math.Round(100.0 * claimsOf.Count / delivered.Count, 1), byPart);
    }

    public async Task<FeedbackReport> FeedbackAsync(Guid shopId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var (shop, f, t) = await RangeAsync(shopId, from, to, ct);
        var rows = await _q.FeedbackAsync(shopId, f, t, ct);
        return new FeedbackReport(Period(f, t, _rates.CreateConverter(shop.ReportingCurrency)), rows.Count,
            rows.Count == 0 ? null : Math.Round(rows.Average(r => r.Score), 2),
            Enumerable.Range(1, 5).ToDictionary(s => s, s => rows.Count(r => r.Score == s)),
            rows.OrderByDescending(r => r.CreatedAtUtc).Take(30).Select(r => new FeedbackComment(RepairOrder.FormatCode(r.OrderNumber), r.Score, r.Comment, r.CreatedAtUtc)).ToList());
    }

    public async Task<InventoryReport> InventoryAsync(Guid shopId, CancellationToken ct)
    {
        var stock = await _q.StockAsync(shopId, ct);
        var rows = stock.Select(s =>
        {
            var value = s.TrackStock && s.UnitCost is not null ? Money.Round(s.UnitCost.Value * Math.Max(0, s.OnHand)) : (decimal?)null;
            return new InventoryValuationRow(s.Id, s.Sku, s.Name, s.Category, s.OnHand, s.Reserved, s.MinStock, s.UnitCost, s.Currency, value,
                s.TrackStock && s.OnHand - s.Reserved <= s.MinStock);
        }).ToList();
        return new InventoryReport(rows,
            rows.Where(r => r.Value is not null).GroupBy(r => r.Currency ?? "?").Select(g => new CurrencyAmount(g.Key, Money.Round(g.Sum(x => x.Value!.Value)))).ToList(),
            rows.Count(r => r.LowStock));
    }

    // ===== Excel export =====

    public async Task<byte[]> ExportAsync(Guid shopId, string report, DateTime? from, DateTime? to, string? groupBy, CancellationToken ct)
    {
        IReadOnlyList<SheetData> sheets = (report ?? "").ToLowerInvariant() switch
        {
            "revenue" => await RevenueSheetsAsync(shopId, from, to, groupBy ?? "day", ct),
            "margins" => MarginSheets(await MarginsAsync(shopId, from, to, ct)),
            "repair-times" => RepairTimesSheets(await RepairTimesAsync(shopId, from, to, ct)),
            "top-issues" => TopSheets(await TopIssuesAsync(shopId, from, to, ct)),
            "technicians" => TechSheets(await TechniciansAsync(shopId, from, to, ct)),
            "warranty" => WarrantySheets(await WarrantyAsync(shopId, from, to, ct)),
            "feedback" => FeedbackSheets(await FeedbackAsync(shopId, from, to, ct)),
            "inventory" => InventorySheets(await InventoryAsync(shopId, ct)),
            "quotes" => QuoteSheets(await QuotesAsync(shopId, from, to, ct)),
            _ => throw new NotFoundException("Reporte inexistente.")
        };
        return _excel.Export(sheets);
    }

    private async Task<IReadOnlyList<SheetData>> RevenueSheetsAsync(Guid shopId, DateTime? from, DateTime? to, string groupBy, CancellationToken ct)
    {
        var r = await RevenueAsync(shopId, from, to, groupBy, ct);
        return new[]
        {
            new SheetData("Ingresos", new[] { "Período", "Moneda", "Medio", "Órdenes", "Ventas", "Devoluciones", "Neto", $"Neto ({r.Period.ReportingCurrency})" },
                r.Rows.Select(x => new object?[] { x.Period, x.Currency, x.Method, x.Orders, x.Sales, x.Refunds, x.Net, x.NetConverted }).ToList())
        };
    }

    private static IReadOnlyList<SheetData> MarginSheets(MarginReport r) => new[]
    {
        new SheetData("Márgenes", new[] { "Tipo", "Código", "Detalle", "Fecha", "Moneda", "Ingreso", "Costo", "Margen", "Margen %" },
            r.Rows.Select(x => new object?[] { x.Kind == "order" ? "Orden" : "Venta", x.Code, x.Description, x.DateUtc, x.Currency, x.Revenue, x.Cost, x.Margin, x.MarginPercent }).ToList())
    };

    private static IReadOnlyList<SheetData> RepairTimesSheets(RepairTimesReport r) => new[]
    {
        new SheetData("Tiempos por estado", new[] { "Estado", "Órdenes", "Promedio (h)", "Mediana (h)" },
            r.ByStatus.Select(x => new object?[] { x.Label, x.Orders, x.AverageHours, x.MedianHours }).ToList())
    };

    private static IReadOnlyList<SheetData> TopSheets(TopIssuesReport r) => new[]
    {
        new SheetData("Por categoría", new[] { "Categoría", "Órdenes", "Ticket promedio", "Moneda" }, r.ByCategory.Select(x => new object?[] { x.Key, x.Orders, x.AverageTicket, x.Currency }).ToList()),
        new SheetData("Por modelo", new[] { "Modelo", "Órdenes", "Ticket promedio", "Moneda" }, r.ByModel.Select(x => new object?[] { x.Key, x.Orders, x.AverageTicket, x.Currency }).ToList()),
        new SheetData("Por marca", new[] { "Marca", "Órdenes", "Ticket promedio", "Moneda" }, r.ByBrand.Select(x => new object?[] { x.Key, x.Orders, x.AverageTicket, x.Currency }).ToList())
    };

    private static IReadOnlyList<SheetData> TechSheets(TechniciansReport r) => new[]
    {
        new SheetData("Técnicos", new[] { "Técnico", "Asignadas", "Entregadas", "Horas a listo (prom.)", "Facturado", "Reingresos garantía", "Tasa reingreso %" },
            r.Rows.Select(x => new object?[] { x.Name, x.Assigned, x.Delivered, x.AverageHoursToReady, string.Join(" / ", x.Revenue.Select(v => $"{v.Amount} {v.Currency}")), x.WarrantyClaims, x.ReentryRate }).ToList())
    };

    private static IReadOnlyList<SheetData> WarrantySheets(WarrantyReport r) => new[]
    {
        new SheetData("Garantías por repuesto", new[] { "SKU", "Repuesto", "Usos", "Reingresos", "Tasa %" },
            r.ByPart.Select(x => new object?[] { x.Sku, x.Name, x.Uses, x.Claims, x.ClaimRate }).ToList())
    };

    private static IReadOnlyList<SheetData> FeedbackSheets(FeedbackReport r) => new[]
    {
        new SheetData("Encuestas", new[] { "Orden", "Puntaje", "Comentario", "Fecha" }, r.Latest.Select(x => new object?[] { x.OrderCode, x.Score, x.Comment, x.CreatedAtUtc }).ToList())
    };

    private static IReadOnlyList<SheetData> InventorySheets(InventoryReport r) => new[]
    {
        new SheetData("Inventario", new[] { "SKU", "Nombre", "Categoría", "Stock", "Reservado", "Mínimo", "Costo unit.", "Moneda", "Valorizado", "Stock bajo" },
            r.Rows.Select(x => new object?[] { x.Sku, x.Name, x.Category, x.OnHand, x.Reserved, x.MinStock, x.UnitCost, x.Currency, x.Value, x.LowStock ? "Sí" : "No" }).ToList())
    };

    private static IReadOnlyList<SheetData> QuoteSheets(QuoteStatsReport r) => new[]
    {
        new SheetData("Presupuestos", new[] { "Total", "Aprobados", "Rechazados", "Vencidos", "Pendientes", "Tasa de aprobación %" },
            new List<object?[]> { new object?[] { r.Total, r.Approved, r.Rejected, r.Expired, r.Pending, r.ApprovalRate } })
    };

    // ---------------------------------------------------------------------------------------------

    private async Task<(Shop Shop, DateTime From, DateTime To)> RangeAsync(Guid shopId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var now = _clock.UtcNow;
        var t = to?.ToUniversalTime() ?? now;
        var f = from?.ToUniversalTime() ?? DashboardService.LocalBoundaries(shop, now).MonthStartUtc;
        if (f > t) throw new DomainException("La fecha desde no puede ser posterior a la fecha hasta.");
        if ((t - f).TotalDays > 732) throw new DomainException("El rango máximo de un reporte es de 2 años.");
        return (shop, f, t);
    }

    private static ReportPeriod Period(DateTime f, DateTime t, CurrencyConverter c)
        => new(f, t, c.Target, c.SourcesUsed.ToList(), c.MissingCurrencies.ToList());

    private static void Add(Dictionary<RepairOrderStatus, List<double>> map, RepairOrderStatus status, double hours)
    {
        if (!map.TryGetValue(status, out var list)) map[status] = list = new List<double>();
        list.Add(Math.Max(0, hours));
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }
}
