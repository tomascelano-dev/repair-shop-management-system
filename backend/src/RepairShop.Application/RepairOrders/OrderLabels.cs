using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.RepairOrders;

public static class OrderLabels
{
    public static string Status(RepairOrderStatus s) => s switch
    {
        RepairOrderStatus.Received => "Recibido",
        RepairOrderStatus.Diagnosing => "En diagnóstico",
        RepairOrderStatus.WaitingParts => "Esperando repuesto",
        RepairOrderStatus.InProgress => "En reparación",
        RepairOrderStatus.Testing => "En pruebas",
        RepairOrderStatus.Ready => "Listo para retirar",
        RepairOrderStatus.Delivered => "Entregado",
        RepairOrderStatus.Cancelled => "Cancelado",
        _ => s.ToString()
    };

    public static string Priority(RepairOrderPriority p) => p switch
    {
        RepairOrderPriority.Low => "Baja",
        RepairOrderPriority.Normal => "Normal",
        RepairOrderPriority.High => "Alta",
        RepairOrderPriority.Urgent => "Urgente",
        _ => p.ToString()
    };

    public static string Quote(QuoteStatus s) => s switch
    {
        QuoteStatus.Draft => "Borrador",
        QuoteStatus.Sent => "Enviado",
        QuoteStatus.Approved => "Aprobado",
        QuoteStatus.Rejected => "Rechazado",
        QuoteStatus.Expired => "Vencido",
        QuoteStatus.Superseded => "Reemplazado",
        _ => s.ToString()
    };

    /// <summary>Message template sent when an order enters a status.</summary>
    public static string StatusTemplateKey(RepairOrderStatus s) => s switch
    {
        RepairOrderStatus.Received => "order.status.received",
        RepairOrderStatus.Diagnosing => "order.diagnosis.started",
        RepairOrderStatus.WaitingParts => "order.status.waiting_parts",
        RepairOrderStatus.InProgress => "order.status.inprogress",
        RepairOrderStatus.Testing => "order.status.testing",
        RepairOrderStatus.Ready => "order.status.ready",
        RepairOrderStatus.Delivered => "order.status.delivered",
        RepairOrderStatus.Cancelled => "order.status.cancelled",
        _ => "order.status." + s.ToString().ToLowerInvariant()
    };
}

public static class TemplateKeys
{
    public const string QuoteSent = "order.quote.sent";
    public const string QuoteApproved = "order.quote.approved";
    public const string QuoteRejected = "order.quote.rejected";
    public const string PaymentRequest = "order.payment.request";
    public const string WarrantyInfo = "order.warranty.info";
    public const string ReadyReminder = "order.reminder.ready_pickup";
    public const string FeedbackRequest = "order.feedback.request";
}
