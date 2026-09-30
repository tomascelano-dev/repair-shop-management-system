using System.Text.RegularExpressions;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.RepairOrders;

public sealed class RenderOrderMessageService
{
    private static readonly Regex CurlyToken = new(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex PercentToken = new(@"%([a-zA-Z0-9_]+)%", RegexOptions.Compiled);

    private readonly IShopRepository _shops;
    private readonly IMessageTemplateRepository _templates;
    private readonly IRepairOrderRepository _orders;
    private readonly ICustomerRepository _customers;
    private readonly IDeviceRepository _devices;
    private readonly IRepairOrderPaymentRepository _payments;
    private readonly IRepairOrderReceptionChecklistRepository _checklists;

    public RenderOrderMessageService(
        IShopRepository shops,
        IMessageTemplateRepository templates,
        IRepairOrderRepository orders,
        ICustomerRepository customers,
        IDeviceRepository devices,
        IRepairOrderPaymentRepository payments,
        IRepairOrderReceptionChecklistRepository checklists)
    {
        _shops = shops;
        _templates = templates;
        _orders = orders;
        _customers = customers;
        _devices = devices;
        _payments = payments;
        _checklists = checklists;
    }

    public async Task<MessagePreviewResponse> RenderAsync(Guid shopId, Guid orderId, string templateKey, bool allowFallback, CancellationToken ct)
    {
        templateKey = (templateKey ?? "").Trim().ToLowerInvariant();
        if (templateKey.Length < 3) throw new NotFoundException("Template key is required.");

        var template = await _templates.GetByKeyAsync(shopId, templateKey, ct);
        if (template is null)
        {
            if (!allowFallback) throw new NotFoundException($"Template not found: '{templateKey}'.");
            return new MessagePreviewResponse(templateKey, "Auto", BuildFallbackMessage(orderId, templateKey));
        }

        var order = await _orders.GetByIdAsync(shopId, orderId, ct);
        if (order is null)
        {
            if (!allowFallback) throw new NotFoundException("Order not found.");
            return new MessagePreviewResponse(templateKey, template.Title, template.Body);
        }

        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        var device = await _devices.GetByIdAsync(shopId, order.DeviceId, ct);
        var shop = await _shops.GetByIdAsync(shopId, ct);

        var paidTotal = await _payments.SumByOrderAsync(shopId, orderId, ct);
        var checklist = await _checklists.GetByOrderAsync(shopId, orderId, ct);

        var tokens = BuildTokens(shop, order, customer, device, paidTotal, checklist);
        var body = ReplaceTokens(template.Body, tokens);

        return new MessagePreviewResponse(template.Key, template.Title, body);
    }

    private static string BuildFallbackMessage(Guid orderId, string templateKey)
        => $"[TEMPLATE MISSING: {templateKey}] Pedido {orderId}";

    private static Dictionary<string, string> BuildTokens(
        RepairShop.Domain.Shops.Shop? shop,
        RepairOrder order,
        RepairShop.Domain.Customers.Customer? customer,
        RepairShop.Domain.Devices.Device? device,
        decimal paidTotal,
        RepairOrderReceptionChecklist? checklist)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        dict["order_id"] = order.Id.ToString();
        dict["order_status"] = order.Status.ToString();
        dict["order_created_at"] = order.CreatedAtUtc.ToString("yyyy-MM-dd");
        dict["order_updated_at"] = order.UpdatedAtUtc.ToString("yyyy-MM-dd");
        dict["issue_description"] = order.IssueDescription;
        dict["notes"] = order.Notes ?? "";

        dict["quote_amount"] = order.QuoteAmount?.ToString("0.00") ?? "";
        dict["quote_currency"] = order.QuoteCurrency ?? "";
        dict["paid_total"] = paidTotal.ToString("0.00");

        var quote = order.QuoteAmount ?? 0m;
        dict["balance_due"] = (quote - paidTotal).ToString("0.00");

        if (customer is not null)
        {
            dict["customer_name"] = customer.FullName;
            dict["customer_phone"] = customer.Phone;
        }
        else
        {
            dict["customer_name"] = "";
            dict["customer_phone"] = "";
        }

        if (device is not null)
        {
            dict["device_brand"] = device.Brand;
            dict["device_model"] = device.Model;
            dict["device_label"] = device.Label ?? "";
            dict["device_serial"] = device.SerialNumber ?? "";
        }
        else
        {
            dict["device_brand"] = "";
            dict["device_model"] = "";
            dict["device_label"] = "";
            dict["device_serial"] = "";
        }

        if (shop is not null)
        {
            dict["shop_name"] = shop.Name;
            dict["shop_phone"] = shop.Phone ?? "";
            dict["shop_address"] = shop.AddressLine ?? "";
            dict["shop_city"] = shop.City ?? "";
            dict["shop_country"] = shop.Country ?? "";
        }
        else
        {
            dict["shop_name"] = "";
            dict["shop_phone"] = "";
            dict["shop_address"] = "";
            dict["shop_city"] = "";
            dict["shop_country"] = "";
        }

        if (checklist is not null)
        {
            dict["check_screen_ok"] = checklist.ScreenOk ? "SI" : "NO";
            dict["check_cameras_ok"] = checklist.CamerasOk ? "SI" : "NO";
            dict["check_speakers_ok"] = checklist.SpeakersOk ? "SI" : "NO";
            dict["check_microphone_ok"] = checklist.MicrophoneOk ? "SI" : "NO";
            dict["check_buttons_ok"] = checklist.ButtonsOk ? "SI" : "NO";
            dict["check_faceid_ok"] = checklist.FaceIdOk ? "SI" : "NO";
            dict["check_fingerprint_ok"] = checklist.FingerprintOk ? "SI" : "NO";
            dict["check_cloud_lock"] = checklist.CloudLock.ToString();
            dict["check_battery_percent"] = checklist.BatteryPercent?.ToString() ?? "";
            dict["check_cosmetic_notes"] = checklist.CosmeticNotes ?? "";
        }
        else
        {
            dict["check_screen_ok"] = "";
            dict["check_cameras_ok"] = "";
            dict["check_speakers_ok"] = "";
            dict["check_microphone_ok"] = "";
            dict["check_buttons_ok"] = "";
            dict["check_faceid_ok"] = "";
            dict["check_fingerprint_ok"] = "";
            dict["check_cloud_lock"] = "";
            dict["check_battery_percent"] = "";
            dict["check_cosmetic_notes"] = "";
        }

        return dict;
    }

    private static string ReplaceTokens(string input, Dictionary<string, string> tokens)
    {
        input ??= "";

        var out1 = CurlyToken.Replace(input, m =>
        {
            var key = m.Groups[1].Value;
            return tokens.TryGetValue(key, out var v) ? v : m.Value;
        });

        var out2 = PercentToken.Replace(out1, m =>
        {
            var key = m.Groups[1].Value;
            return tokens.TryGetValue(key, out var v) ? v : m.Value;
        });

        return out2;
    }
}
