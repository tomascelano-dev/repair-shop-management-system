using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Devices;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Shops;

namespace RepairShop.Application.RepairOrders;

/// <summary>
/// Renders message templates for an order replacing {{tokens}} (and legacy %tokens%).
/// </summary>
public sealed class RenderOrderMessageService
{
    private static readonly Regex CurlyToken = new(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex PercentToken = new(@"%([a-zA-Z0-9_]+)%", RegexOptions.Compiled);
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    // Argentine number format (1.234,56) without depending on ICU culture data (InvariantGlobalization).
    public static readonly NumberFormatInfo ArNumbers = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    private readonly IShopRepository _shops;
    private readonly IMessageTemplateRepository _templates;
    private readonly IRepairOrderRepository _orders;
    private readonly ICustomerRepository _customers;
    private readonly IDeviceRepository _devices;
    private readonly IRepairOrderReceptionChecklistRepository _checklists;
    private readonly IQuoteRepository _quotes;
    private readonly IUserRepository _users;
    private readonly IRepairOrderReadModel _readModel;
    private readonly IAppLinks _links;

    public RenderOrderMessageService(
        IShopRepository shops,
        IMessageTemplateRepository templates,
        IRepairOrderRepository orders,
        ICustomerRepository customers,
        IDeviceRepository devices,
        IRepairOrderReceptionChecklistRepository checklists,
        IQuoteRepository quotes,
        IUserRepository users,
        IRepairOrderReadModel readModel,
        IAppLinks links)
    {
        _shops = shops;
        _templates = templates;
        _orders = orders;
        _customers = customers;
        _devices = devices;
        _checklists = checklists;
        _quotes = quotes;
        _users = users;
        _readModel = readModel;
        _links = links;
    }

    public async Task<MessagePreviewResponse> RenderAsync(Guid shopId, Guid orderId, string templateKey, bool allowFallback, CancellationToken ct)
    {
        templateKey = (templateKey ?? "").Trim().ToLowerInvariant();
        if (templateKey.Length < 3) throw new NotFoundException("Falta la clave de la plantilla.");

        var template = await _templates.GetByKeyAsync(shopId, templateKey, ct);
        if (template is null || !template.IsActive)
        {
            if (!allowFallback) throw new NotFoundException($"No existe la plantilla '{templateKey}' (o está inactiva).");
        }

        var order = await _orders.GetByIdAsync(shopId, orderId, ct);
        if (order is null)
        {
            if (!allowFallback) throw new NotFoundException("Orden no encontrada.");
            return new MessagePreviewResponse(templateKey, template?.Title ?? "Aviso", template?.Body ?? "");
        }

        var tokens = await BuildTokensAsync(shopId, order, ct);
        var title = template?.Title ?? "Aviso";
        var body = template is null ? BuildFallbackMessage(order, tokens) : ReplaceTokens(template.Body, tokens);
        var phone = tokens.GetValueOrDefault("customer_whatsapp", "");
        return new MessagePreviewResponse(templateKey, ReplaceTokens(title, tokens), body,
            string.IsNullOrEmpty(phone) ? null : WhatsAppLink(phone, body), tokens.GetValueOrDefault("customer_phone"));
    }

    public static string WhatsAppLink(string digits, string text)
        => $"https://wa.me/{digits}?text={Uri.EscapeDataString(text ?? "")}";

    public async Task<Dictionary<string, string>> BuildTokensAsync(Guid shopId, RepairOrder order, CancellationToken ct)
    {
        var shop = await _shops.GetByIdAsync(shopId, ct);
        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        var device = await _devices.GetByIdAsync(shopId, order.DeviceId, ct);
        var checklist = await _checklists.GetByOrderAsync(shopId, order.Id, ct);
        var quotes = await _quotes.ListByOrderAsync(shopId, order.Id, ct);
        var tech = order.AssignedTechnicianId is null ? null : await _users.GetByIdAsync(order.AssignedTechnicianId.Value, ct);
        var info = (await _readModel.GetInfoAsync(shopId, new[] { order.Id }, ct)).GetValueOrDefault(order.Id);

        var tz = ResolveTimeZone(shop?.TimeZone);
        string Date(DateTime? utc) => utc is null ? "" : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), tz).ToString("dd/MM/yyyy", Es);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["order_id"] = order.Id.ToString(),
            ["order_code"] = order.Code,
            ["order_number"] = order.OrderNumber.ToString(Es),
            ["order_status"] = order.Status.ToString(),
            ["order_status_label"] = OrderLabels.Status(order.Status),
            ["order_created_at"] = Date(order.CreatedAtUtc),
            ["order_updated_at"] = Date(order.UpdatedAtUtc),
            ["promised_date"] = Date(order.PromisedAtUtc),
            ["issue_description"] = order.IssueDescription,
            ["notes"] = order.Notes ?? "",
            ["tracking_url"] = _links.Tracking(order.PublicToken),
            ["feedback_url"] = _links.Feedback(order.PublicToken),
            ["technician_name"] = tech?.DisplayName ?? "el equipo técnico",
            ["warranty_days"] = (order.WarrantyDays ?? shop?.DefaultWarrantyDays ?? 0).ToString(Es),
            ["warranty_expires_at"] = Date(order.WarrantyExpiresAtUtc),
            ["cancellation_reason"] = order.CancellationReason ?? ""
        };

        // Money: agreed price + extras - payments.
        var money = info is null ? null : OrderMapping.Financials(order, info, shop?.DefaultCurrency ?? "ARS");
        var currency = money?.Currency ?? order.QuoteCurrency ?? shop?.DefaultCurrency ?? "ARS";
        dict["paid_total"] = Format(money?.Paid ?? 0m);
        dict["balance_due"] = Format(Math.Max(0, money?.BalanceDue ?? 0m));
        dict["order_total"] = Format(money?.Total ?? order.QuoteAmount ?? 0m);
        dict["currency"] = currency;

        // Quote: the most relevant one (open > approved > latest).
        var quote = quotes.FirstOrDefault(q => q.IsOpen)
                    ?? quotes.FirstOrDefault(q => q.Status == QuoteStatus.Approved)
                    ?? quotes.FirstOrDefault();
        dict["quote_amount"] = quote is not null ? Format(quote.Total) : order.QuoteAmount is null ? "" : Format(order.QuoteAmount.Value);
        dict["quote_currency"] = quote?.Currency ?? order.QuoteCurrency ?? currency;
        dict["quote_valid_until"] = Date(quote?.ValidUntilUtc);
        dict["quote_items"] = quote is null ? "" : string.Join("\n", quote.Items.Select(i => $"• {i.Description}: {Format(i.LineTotal)} {quote.Currency}"));
        if (quote?.WarrantyDays is not null) dict["warranty_days"] = quote.WarrantyDays.Value.ToString(Es);

        AddCustomer(dict, customer, shop);
        AddDevice(dict, device);
        AddShop(dict, shop);
        AddChecklist(dict, checklist);
        return dict;
    }

    private static void AddCustomer(Dictionary<string, string> dict, Customer? customer, Shop? shop)
    {
        dict["customer_name"] = customer?.FullName ?? "";
        dict["customer_first_name"] = customer?.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        dict["customer_phone"] = customer?.Phone ?? "";
        dict["customer_whatsapp"] = customer is null ? "" : PhoneNumber.ToWhatsAppDigits(customer.Phone, shop?.PhoneCountryCode ?? "54");
    }

    private static void AddDevice(Dictionary<string, string> dict, Device? device)
    {
        dict["device_brand"] = device?.Brand ?? "";
        dict["device_model"] = device?.Model ?? "";
        dict["device_label"] = device?.Label ?? "";
        dict["device_serial"] = device?.SerialNumber ?? device?.Imei ?? "-";
        dict["device_imei"] = device?.Imei ?? "";
    }

    private static void AddShop(Dictionary<string, string> dict, Shop? shop)
    {
        dict["shop_name"] = shop?.Name ?? "";
        dict["shop_phone"] = shop?.Phone ?? "";
        dict["shop_whatsapp"] = shop?.Phone is null ? "" : PhoneNumber.ToWhatsAppDigits(shop.Phone, shop.PhoneCountryCode);
        dict["shop_address"] = shop?.AddressLine ?? "";
        dict["shop_city"] = shop?.City ?? "";
        dict["shop_country"] = shop?.Country ?? "";
        dict["pickup_address"] = string.Join(", ", new[] { shop?.AddressLine, shop?.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
        dict["pickup_hours"] = shop?.PickupHours ?? "";
        dict["google_review_url"] = shop?.GoogleReviewUrl ?? "";
    }

    private static void AddChecklist(Dictionary<string, string> dict, RepairOrderReceptionChecklist? checklist)
    {
        string YesNo(bool? v) => v is null ? "" : v.Value ? "SI" : "NO";
        dict["check_screen_ok"] = YesNo(checklist?.ScreenOk);
        dict["check_cameras_ok"] = YesNo(checklist?.CamerasOk);
        dict["check_speakers_ok"] = YesNo(checklist?.SpeakersOk);
        dict["check_microphone_ok"] = YesNo(checklist?.MicrophoneOk);
        dict["check_buttons_ok"] = YesNo(checklist?.ButtonsOk);
        dict["check_faceid_ok"] = YesNo(checklist?.FaceIdOk);
        dict["check_fingerprint_ok"] = YesNo(checklist?.FingerprintOk);
        dict["check_cloud_lock"] = checklist?.CloudLock.ToString() ?? "";
        dict["check_battery_percent"] = checklist?.BatteryPercent?.ToString(Es) ?? "";
        dict["check_cosmetic_notes"] = checklist?.CosmeticNotes ?? "";
    }

    private static string BuildFallbackMessage(RepairOrder order, IReadOnlyDictionary<string, string> tokens)
    {
        var sb = new StringBuilder();
        sb.Append("Hola ").Append(tokens.GetValueOrDefault("customer_first_name", "")).Append(" 👋\n");
        sb.Append("Tu orden ").Append(order.Code).Append(" está: ").Append(OrderLabels.Status(order.Status)).Append(".\n");
        sb.Append("Seguimiento: ").Append(tokens.GetValueOrDefault("tracking_url", "")).Append("\n— ").Append(tokens.GetValueOrDefault("shop_name", ""));
        return sb.ToString();
    }

    public static string ReplaceTokens(string input, IReadOnlyDictionary<string, string> tokens)
    {
        input ??= "";
        var out1 = CurlyToken.Replace(input, m => tokens.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        return PercentToken.Replace(out1, m => tokens.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }

    public static string Format(decimal amount) => amount.ToString("#,0.00", ArNumbers);

    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? Shop.DefaultTimeZone : id); }
        catch { return TimeZoneInfo.CreateCustomTimeZone("AR", TimeSpan.FromHours(-3), "UTC-03", "UTC-03"); }
    }
}
