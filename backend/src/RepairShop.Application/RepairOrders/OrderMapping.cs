using RepairShop.Application.Abstractions;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.RepairOrders;

public static class OrderMapping
{
    public static OrderFinancialsResponse Financials(RepairOrder o, OrderReadInfo info, string defaultCurrency)
    {
        var currency = o.QuoteCurrency ?? info.PaymentsCurrency ?? defaultCurrency;
        var agreed = o.QuoteAmount ?? 0m;
        var total = Money.Round(agreed + info.ExtraCharges);
        return new OrderFinancialsResponse(currency, agreed, info.ExtraCharges, total, info.Paid, Money.Round(total - info.Paid), o.QuoteAmount is not null);
    }

    public static RepairOrderResponse ToResponse(RepairOrder o, OrderReadInfo info, string defaultCurrency, IAppLinks links, DateTime nowUtc)
    {
        var money = Financials(o, info, defaultCurrency);
        return new RepairOrderResponse(
            o.Id, o.ShopId, o.CustomerId, o.DeviceId, o.IssueDescription, o.Notes, o.Status.ToString(),
            o.QuoteAmount, o.QuoteCurrency, o.QuoteUpdatedByUserId, o.QuoteUpdatedAtUtc, o.CreatedAtUtc, o.UpdatedAtUtc,
            o.OrderNumber, o.Code, OrderLabels.Status(o.Status), o.IssueCategory, o.Priority.ToString(),
            o.AssignedTechnicianId, info.TechnicianName, o.PromisedAtUtc, o.IsOverdue(nowUtc),
            info.CustomerName, info.CustomerPhone, info.DeviceDisplay, info.DeviceImei,
            money.Currency, money.Total, money.Paid, money.BalanceDue, money.ExtraCharges,
            info.HasApprovedQuote || o.QuoteAmount is not null, info.QaPassed,
            o.WarrantyDays, o.WarrantyExpiresAtUtc, o.IsUnderWarranty(nowUtc), o.IsWarrantyClaim, o.WarrantyOfOrderId,
            o.LastStatusChangeAtUtc, o.ReadyAtUtc, o.DeliveredAtUtc, o.CancelledAtUtc, o.CancellationReason,
            o.UnlockMethod.ToString(), o.UnlockSecretProtected is not null,
            o.ReceptionSignatureFileId is not null, o.ReceptionSignedByName,
            o.DeliverySignatureFileId is not null, o.DeliverySignedByName,
            links.Tracking(o.PublicToken),
            RepairOrder.AllowedTransitions(o.Status).Select(s => s.ToString()).ToList(),
            info.PhotosCount);
    }

    public static OrderCardResponse ToCard(RepairOrder o, OrderReadInfo info, string defaultCurrency, int staleDays, DateTime nowUtc)
    {
        var money = Financials(o, info, defaultCurrency);
        return new OrderCardResponse(
            o.Id, o.Code, o.Status.ToString(), info.CustomerName, info.DeviceDisplay, o.IssueDescription, o.Priority.ToString(),
            o.AssignedTechnicianId, info.TechnicianName, o.PromisedAtUtc, o.IsOverdue(nowUtc),
            !o.IsFinal && o.LastStatusChangeAtUtc < nowUtc.AddDays(-staleDays),
            (int)Math.Floor((nowUtc - o.CreatedAtUtc).TotalDays), o.LastStatusChangeAtUtc, o.IsWarrantyClaim,
            money.BalanceDue, money.Currency);
    }
}
