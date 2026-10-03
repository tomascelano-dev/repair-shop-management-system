using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Inventory;
using RepairShop.Domain.Common;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Suggestions;

/// <summary>
/// Suggests diagnosis and quote items from similar past repairs (same brand/model + text similarity),
/// optionally enriched by an LLM. Works fully offline when no AI provider is configured.
/// </summary>
public sealed class SuggestionService
{
    private static readonly Regex Word = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "el","la","los","las","un","una","unos","unas","de","del","al","a","y","o","que","en","con","por","para","no","se","su","sus","lo",
        "le","les","es","esta","está","muy","mas","más","pero","sin","cuando","como","ya","tiene","tenia","tenía","anda","equipo","celular","telefono","teléfono"
    };

    private readonly IRepairOrderRepository _orders;
    private readonly IDeviceRepository _devices;
    private readonly IQuoteRepository _quotes;
    private readonly IRepairOrderReceptionChecklistRepository _checklists;
    private readonly IInventoryItemRepository _items;
    private readonly IInventoryCompatibilityRepository _compat;
    private readonly InventoryService _inventory;
    private readonly IShopRepository _shops;
    private readonly IAiAssistant _ai;
    private readonly ILogger<SuggestionService> _logger;

    public SuggestionService(
        IRepairOrderRepository orders,
        IDeviceRepository devices,
        IQuoteRepository quotes,
        IRepairOrderReceptionChecklistRepository checklists,
        IInventoryItemRepository items,
        IInventoryCompatibilityRepository compat,
        InventoryService inventory,
        IShopRepository shops,
        IAiAssistant ai,
        ILogger<SuggestionService> logger)
    {
        _orders = orders;
        _devices = devices;
        _quotes = quotes;
        _checklists = checklists;
        _items = items;
        _compat = compat;
        _inventory = inventory;
        _shops = shops;
        _ai = ai;
        _logger = logger;
    }

    public async Task<RepairSuggestionResponse> GetAsync(Guid shopId, Guid orderId, bool includeAi, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(shopId, orderId, ct) ?? throw new NotFoundException("Orden no encontrada.");
        var device = await _devices.GetByIdAsync(shopId, order.DeviceId, ct) ?? throw new NotFoundException("Equipo no encontrado.");
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");

        // Candidates: recent orders of the same brand (any status except cancelled), excluding this one.
        var (candidates, _) = await _orders.SearchAsync(shopId, new RepairOrderSearchOptions(Q: device.Brand, Take: 200, SortBy: "createdAt", SortDir: "desc"), ct);
        candidates = candidates.Where(o => o.Id != order.Id && o.Status != RepairOrderStatus.Cancelled).ToList();

        var tokens = Tokens(order.IssueDescription + " " + order.IssueCategory);
        var scored = new List<(RepairOrder Order, Domain.Devices.Device Device, double Score)>();
        foreach (var o in candidates)
        {
            var d = await _devices.GetByIdAsync(shopId, o.DeviceId, ct);
            if (d is null || !string.Equals(d.Brand, device.Brand, StringComparison.OrdinalIgnoreCase)) continue;

            var modelScore = string.Equals(d.Model, device.Model, StringComparison.OrdinalIgnoreCase) ? 1.0 : Jaccard(Tokens(d.Model), Tokens(device.Model)) * 0.6;
            var textScore = Jaccard(tokens, Tokens(o.IssueDescription + " " + o.IssueCategory));
            var categoryBonus = !string.IsNullOrWhiteSpace(order.IssueCategory) && string.Equals(order.IssueCategory, o.IssueCategory, StringComparison.OrdinalIgnoreCase) ? 0.15 : 0;
            var score = 0.45 * modelScore + 0.55 * textScore + categoryBonus;
            if (score >= 0.2) scored.Add((o, d, Math.Min(1, score)));
        }

        var top = scored.OrderByDescending(x => x.Score).Take(8).ToList();
        var similar = new List<SimilarOrderResponse>();
        var itemSamples = new List<(QuoteItem Item, string Currency)>();
        var totals = new List<(decimal Total, string Currency)>();

        foreach (var (o, d, score) in top)
        {
            var approved = await _quotes.GetApprovedAsync(shopId, o.Id, ct);
            if (approved is not null)
            {
                itemSamples.AddRange(approved.Items.Select(i => (i, approved.Currency)));
                if (approved.Total > 0) totals.Add((approved.Total, approved.Currency));
            }
            else if (o.QuoteAmount is > 0 && o.QuoteCurrency is not null)
            {
                totals.Add((o.QuoteAmount.Value, o.QuoteCurrency));
            }

            similar.Add(new SimilarOrderResponse(o.Id, o.Code, $"{d.Brand} {d.Model}", o.IssueDescription, o.IssueCategory, o.Status.ToString(),
                approved?.Total ?? o.QuoteAmount, approved?.Currency ?? o.QuoteCurrency,
                o.ReadyAtUtc is null ? null : Math.Round((o.ReadyAtUtc.Value - o.CreatedAtUtc).TotalHours, 1),
                Math.Round(score, 2), approved?.Items.Select(i => i.Description).ToList() ?? new List<string>()));
        }

        var mainCurrency = totals.GroupBy(t => t.Currency).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? order.QuoteCurrency ?? shop.DefaultCurrency;
        var suggested = itemSamples.Where(x => x.Currency == mainCurrency)
            .GroupBy(x => NormalizeDescription(x.Item.Description))
            .Select(g => new SuggestedItemResponse(g.First().Item.Kind.ToString(), g.First().Item.Description, g.Count(),
                Money.Round(Median(g.Select(x => x.Item.UnitPrice).ToList())), mainCurrency, g.Select(x => x.Item.InventoryItemId).FirstOrDefault(id => id is not null)))
            .OrderByDescending(x => x.Frequency).Take(10).ToList();

        var priced = totals.Where(t => t.Currency == mainCurrency).Select(t => t.Total).OrderBy(x => x).ToList();
        var range = priced.Count == 0 ? null : new PriceRangeResponse(priced.First(), Money.Round(Median(priced)), priced.Last(), mainCurrency, priced.Count);

        var compatibleIds = await _compat.FindItemIdsAsync(shopId, device.Brand, device.Model, ct);
        var compatibleItems = compatibleIds.Count == 0 ? new List<InventoryItemResponse>()
            : await _inventory.ToResponsesAsync(shopId, (await _items.GetByIdsAsync(shopId, compatibleIds, ct)).Where(i => i.IsActive).ToList(), ct);

        AiSuggestionResponse? ai = null;
        string? aiError = null;
        if (includeAi && _ai.IsConfigured)
        {
            try
            {
                var checklist = await _checklists.GetByOrderAsync(shopId, orderId, ct);
                var result = await _ai.SuggestRepairAsync(new AiRepairRequest(device.Brand, device.Model, order.IssueDescription, order.IssueCategory, mainCurrency,
                    checklist is null ? null : $"pantalla {(checklist.ScreenOk ? "ok" : "con falla")}, cámaras {(checklist.CamerasOk ? "ok" : "con falla")}, " +
                                                $"audio {(checklist.SpeakersOk ? "ok" : "con falla")}, batería {checklist.BatteryPercent?.ToString() ?? "s/d"}%, notas: {checklist.CosmeticNotes}",
                    similar.Take(5).Select(s => new AiSimilarCase(s.Device, s.IssueDescription, s.Category, s.Items, s.Total, s.Currency)).ToList()), ct);
                if (result is not null)
                    ai = new AiSuggestionResponse(result.DiagnosisHypotheses,
                        result.SuggestedItems.Select(i => new SuggestedItemResponse(i.Kind, i.Description, 0, i.EstimatedPrice, mainCurrency, null)).ToList(),
                        result.CustomerMessage, result.Model);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI suggestion failed for order {OrderId}.", orderId);
                aiError = "No se pudo obtener la sugerencia de IA en este momento.";
            }
        }

        return new RepairSuggestionResponse(similar, suggested, range, compatibleItems, _ai.IsConfigured, ai, aiError);
    }

    private static HashSet<string> Tokens(string? text)
        => Word.Matches((text ?? "").ToLowerInvariant()).Select(m => m.Value).Where(w => w.Length > 1 && !StopWords.Contains(w)).Select(Stem).ToHashSet();

    // Very small Spanish stemmer: plural/gender endings ("pantallas" ~ "pantalla", "rota" ~ "roto").
    private static string Stem(string w)
    {
        if (w.Length > 4 && w.EndsWith("es")) w = w[..^2];
        else if (w.Length > 3 && w.EndsWith('s')) w = w[..^1];
        if (w.Length > 3 && (w.EndsWith('a') || w.EndsWith('o'))) w = w[..^1];
        return w;
    }

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var inter = a.Count(b.Contains);
        return (double)inter / (a.Count + b.Count - inter);
    }

    private static string NormalizeDescription(string d) => string.Join(' ', Tokens(d).OrderBy(x => x));

    private static decimal Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(x => x).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }
}
