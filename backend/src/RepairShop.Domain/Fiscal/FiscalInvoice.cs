using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;

namespace RepairShop.Domain.Fiscal;

// ARCA (ex AFIP) "CbteTipo" codes.
public enum FiscalVoucherType
{
    FacturaA = 1,
    NotaDebitoA = 2,
    NotaCreditoA = 3,
    FacturaB = 6,
    NotaDebitoB = 7,
    NotaCreditoB = 8,
    FacturaC = 11,
    NotaDebitoC = 12,
    NotaCreditoC = 13
}

public enum FiscalInvoiceStatus
{
    Pending = 0,
    Authorized = 1,
    Rejected = 2,
    Error = 3
}

/// <summary>
/// Electronic invoice authorized by ARCA (CAE). Created from a sale or a repair order.
/// </summary>
public sealed class FiscalInvoice : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public string SourceType { get; private set; } = null!;  // "sale" | "repair_order"
    public Guid SourceId { get; private set; }

    public FiscalVoucherType VoucherType { get; private set; }
    public int PointOfSale { get; private set; }
    public long? Number { get; private set; }
    public DateTime IssueDateUtc { get; private set; }

    // 1 = productos, 2 = servicios, 3 = productos y servicios
    public int Concept { get; private set; }

    public CustomerDocumentType ReceiverDocumentType { get; private set; }
    public string? ReceiverDocumentNumber { get; private set; }
    public string ReceiverName { get; private set; } = null!;
    public CustomerTaxCondition ReceiverTaxCondition { get; private set; }

    public decimal NetAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal Total { get; private set; }
    public string Currency { get; private set; } = "ARS";

    public FiscalInvoiceStatus Status { get; private set; } = FiscalInvoiceStatus.Pending;
    public string? Cae { get; private set; }
    public DateTime? CaeDueDate { get; private set; }
    public string? ResultMessage { get; private set; }

    // Credit notes reference the invoice they cancel.
    public Guid? AssociatedInvoiceId { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private FiscalInvoice() { }

    public FiscalInvoice(
        Guid shopId,
        string sourceType,
        Guid sourceId,
        FiscalVoucherType voucherType,
        int pointOfSale,
        int concept,
        CustomerDocumentType receiverDocumentType,
        string? receiverDocumentNumber,
        string receiverName,
        CustomerTaxCondition receiverTaxCondition,
        decimal netAmount,
        decimal vatAmount,
        decimal total,
        Guid? associatedInvoiceId,
        Guid userId,
        DateTime nowUtc)
    {
        if (sourceId == Guid.Empty) throw new DomainException("La factura debe referenciar una venta u orden.");
        if (pointOfSale is <= 0 or > 99999) throw new DomainException("Punto de venta inválido.");
        if (concept is < 1 or > 3) throw new DomainException("Concepto inválido.");
        if (total <= 0) throw new DomainException("El total de la factura debe ser mayor a 0.");
        if (Money.Round(netAmount + vatAmount) != Money.Round(total)) throw new DomainException("Neto + IVA debe ser igual al total.");
        if (userId == Guid.Empty) throw new DomainException("La factura debe tener un usuario.");

        ShopId = shopId;
        SourceType = sourceType;
        SourceId = sourceId;
        VoucherType = voucherType;
        PointOfSale = pointOfSale;
        Concept = concept;
        ReceiverDocumentType = receiverDocumentType;
        ReceiverDocumentNumber = receiverDocumentNumber;
        ReceiverName = string.IsNullOrWhiteSpace(receiverName) ? "Consumidor Final" : receiverName.Trim()[..Math.Min(receiverName.Trim().Length, 160)];
        ReceiverTaxCondition = receiverTaxCondition;
        NetAmount = Money.Round(netAmount);
        VatAmount = Money.Round(vatAmount);
        Total = Money.Round(total);
        AssociatedInvoiceId = associatedInvoiceId;
        IssueDateUtc = nowUtc;
        CreatedByUserId = userId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public bool IsCreditNote => VoucherType is FiscalVoucherType.NotaCreditoA or FiscalVoucherType.NotaCreditoB or FiscalVoucherType.NotaCreditoC;

    public string Code => Number is null ? $"{PointOfSale:D5}-pendiente" : $"{PointOfSale:D5}-{Number:D8}";

    public void MarkAuthorized(long number, string cae, DateTime caeDueDate, string? message, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(cae)) throw new DomainException("CAE inválido.");
        Number = number;
        Cae = cae.Trim();
        CaeDueDate = caeDueDate;
        Status = FiscalInvoiceStatus.Authorized;
        ResultMessage = Truncate(message);
        UpdatedAtUtc = nowUtc;
    }

    public void MarkRejected(string message, DateTime nowUtc)
    {
        Status = FiscalInvoiceStatus.Rejected;
        ResultMessage = Truncate(message);
        UpdatedAtUtc = nowUtc;
    }

    public void MarkError(string message, DateTime nowUtc)
    {
        Status = FiscalInvoiceStatus.Error;
        ResultMessage = Truncate(message);
        UpdatedAtUtc = nowUtc;
    }

    private static string? Truncate(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Length > 2000 ? value[..2000] : value;
}
