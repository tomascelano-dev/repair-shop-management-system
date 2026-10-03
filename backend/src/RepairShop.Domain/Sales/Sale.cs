using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Domain.Sales;

public enum SaleStatus
{
    Completed = 0,
    Voided = 1,
    PartiallyRefunded = 2,
    Refunded = 3
}

public sealed record SaleLineInput(
    Guid? InventoryItemId,
    string Sku,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal? UnitCost,
    bool TrackStock,
    int? WarrantyDays);

public sealed record SalePaymentInput(PaymentMethod Method, decimal Amount, string? Reference);

/// <summary>
/// Counter sale (punto de venta): accessories, parts and services sold without a repair order.
/// Totals are computed here so every client gets the same rounding rules.
/// </summary>
public sealed class Sale : IShopScoped
{
    private readonly List<SaleLine> _lines = new();
    private readonly List<SalePayment> _payments = new();
    private readonly List<SaleRefund> _refunds = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public int Number { get; private set; }
    public SaleStatus Status { get; private set; } = SaleStatus.Completed;

    public Guid? CustomerId { get; private set; }
    public string Currency { get; private set; } = null!;

    public decimal Subtotal { get; private set; }        // sum of line totals (after line discounts)
    public decimal DiscountAmount { get; private set; }  // global discount
    public decimal Total { get; private set; }
    public decimal PaidAmount { get; private set; }      // tendered (can exceed total with cash)
    public decimal ChangeAmount { get; private set; }    // vuelto
    public decimal RefundedAmount { get; private set; }

    public Guid? CashSessionId { get; private set; }
    public string? Notes { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? VoidedAtUtc { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public string? VoidReason { get; private set; }

    public IReadOnlyList<SaleLine> Lines => _lines;
    public IReadOnlyList<SalePayment> Payments => _payments;
    public IReadOnlyList<SaleRefund> Refunds => _refunds;

    private Sale() { }

    public Sale(
        Guid shopId,
        int number,
        Guid? customerId,
        string currency,
        IReadOnlyCollection<SaleLineInput> lines,
        decimal globalDiscount,
        IReadOnlyCollection<SalePaymentInput> payments,
        Guid? cashSessionId,
        string? notes,
        Guid userId,
        DateTime nowUtc)
    {
        if (number <= 0) throw new DomainException("Número de venta inválido.");
        if (userId == Guid.Empty) throw new DomainException("La venta debe tener un usuario.");
        if (lines.Count == 0) throw new DomainException("La venta debe tener al menos un ítem.");
        if (lines.Count > 200) throw new DomainException("La venta admite hasta 200 ítems.");
        if (payments.Count == 0) throw new DomainException("Registrá al menos un pago.");
        if (globalDiscount < 0) throw new DomainException("El descuento no puede ser negativo.");

        ShopId = shopId;
        Number = number;
        CustomerId = customerId == Guid.Empty ? null : customerId;
        Currency = Money.NormalizeCurrency(currency);
        CashSessionId = cashSessionId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 500)];
        CreatedByUserId = userId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;

        var position = 1;
        foreach (var l in lines) _lines.Add(new SaleLine(Id, position++, l));

        Subtotal = Money.Round(_lines.Sum(x => x.LineTotal));
        DiscountAmount = Money.Round(globalDiscount);
        if (DiscountAmount > Subtotal) throw new DomainException("El descuento no puede superar el subtotal.");
        Total = Money.Round(Subtotal - DiscountAmount);

        foreach (var p in payments) _payments.Add(new SalePayment(Id, p));

        PaidAmount = Money.Round(_payments.Sum(x => x.Amount));
        if (PaidAmount < Total) throw new DomainException($"Falta cobrar {Total - PaidAmount:0.00} {Currency}.");

        var change = PaidAmount - Total;
        var cash = _payments.Where(x => x.Method == PaymentMethod.Cash).Sum(x => x.Amount);
        if (change > 0 && change > cash) throw new DomainException("Solo el efectivo puede generar vuelto: el resto de los pagos supera el total.");
        ChangeAmount = Money.Round(change);
    }

    public string Code => $"V-{Number:D6}";

    /// <summary>Net amount collected per payment method (cash already discounts the change).</summary>
    public IReadOnlyDictionary<PaymentMethod, decimal> NetPaymentsByMethod()
    {
        var map = _payments.GroupBy(p => p.Method).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        if (ChangeAmount > 0 && map.ContainsKey(PaymentMethod.Cash)) map[PaymentMethod.Cash] -= ChangeAmount;
        return map.ToDictionary(k => k.Key, v => Money.Round(v.Value));
    }

    /// <summary>
    /// Net unit price of a line after its own discount and a proportional share of the global discount.
    /// </summary>
    public decimal NetUnitPrice(SaleLine line)
    {
        if (line.Quantity == 0) return 0;
        var share = Subtotal == 0 ? 0 : DiscountAmount * (line.LineTotal / Subtotal);
        return (line.LineTotal - share) / line.Quantity;
    }

    public SaleRefund Refund(IReadOnlyDictionary<Guid, int> quantitiesByLine, PaymentMethod method, bool restock, string? reason, Guid userId, Guid? cashSessionId, DateTime nowUtc)
    {
        if (Status is SaleStatus.Voided or SaleStatus.Refunded) throw new DomainException("La venta ya fue anulada o devuelta por completo.");
        if (userId == Guid.Empty) throw new DomainException("La devolución debe tener un usuario.");

        var refund = new SaleRefund(Id, method, restock, reason, userId, cashSessionId, nowUtc);
        foreach (var (lineId, qty) in quantitiesByLine)
        {
            if (qty <= 0) continue;
            var line = _lines.FirstOrDefault(l => l.Id == lineId) ?? throw new DomainException("Línea de venta inexistente.");
            if (line.RefundedQuantity + qty > line.Quantity) throw new DomainException($"No se pueden devolver más unidades de las vendidas en \"{line.Description}\".");
            line.RegisterRefund(qty);
            refund.AddLine(line.Id, qty, Money.Round(NetUnitPrice(line) * qty));
        }

        if (refund.Lines.Count == 0) throw new DomainException("Indicá al menos un ítem a devolver.");

        // Avoid rounding leftovers: the last refund that returns everything returns the exact remaining amount.
        var remaining = Total - RefundedAmount;
        if (_lines.All(l => l.RefundedQuantity == l.Quantity)) refund.AdjustAmount(remaining);
        if (refund.Amount > remaining) refund.AdjustAmount(remaining);

        _refunds.Add(refund);
        RefundedAmount = Money.Round(RefundedAmount + refund.Amount);
        Status = _lines.All(l => l.RefundedQuantity == l.Quantity) ? SaleStatus.Refunded : SaleStatus.PartiallyRefunded;
        UpdatedAtUtc = nowUtc;
        return refund;
    }

    public SaleRefund Void(string reason, PaymentMethod method, Guid userId, Guid? cashSessionId, DateTime nowUtc)
    {
        if (Status != SaleStatus.Completed) throw new DomainException("Solo se puede anular una venta sin devoluciones.");
        reason = (reason ?? "").Trim();
        if (reason.Length < 3) throw new DomainException("Indicá el motivo de la anulación.");

        var refund = Refund(_lines.ToDictionary(l => l.Id, l => l.Quantity), method, restock: true, reason, userId, cashSessionId, nowUtc);
        Status = SaleStatus.Voided;
        VoidedAtUtc = nowUtc;
        VoidedByUserId = userId;
        VoidReason = reason;
        return refund;
    }
}

public sealed class SaleLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SaleId { get; private set; }
    public int Position { get; private set; }
    public Guid? InventoryItemId { get; private set; }
    public string Sku { get; private set; } = "";
    public string Description { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public decimal? UnitCost { get; private set; }
    public bool TrackStock { get; private set; }
    public int? WarrantyDays { get; private set; }
    public int RefundedQuantity { get; private set; }

    private SaleLine() { }

    internal SaleLine(Guid saleId, int position, SaleLineInput input)
    {
        var description = (input.Description ?? "").Trim();
        if (description.Length < 2) throw new DomainException("Cada ítem necesita una descripción.");
        if (input.Quantity <= 0) throw new DomainException("La cantidad debe ser mayor a 0.");
        if (input.UnitPrice < 0) throw new DomainException("El precio no puede ser negativo.");
        if (input.DiscountAmount < 0) throw new DomainException("El descuento no puede ser negativo.");

        SaleId = saleId;
        Position = position;
        InventoryItemId = input.InventoryItemId == Guid.Empty ? null : input.InventoryItemId;
        Sku = (input.Sku ?? "").Trim();
        Description = description.Length > 200 ? description[..200] : description;
        Quantity = input.Quantity;
        UnitPrice = Money.Round(input.UnitPrice);
        DiscountAmount = Money.Round(input.DiscountAmount);
        var gross = Money.Round(UnitPrice * Quantity);
        if (DiscountAmount > gross) throw new DomainException($"El descuento supera el importe de \"{Description}\".");
        LineTotal = Money.Round(gross - DiscountAmount);
        UnitCost = input.UnitCost is null ? null : Money.Round(input.UnitCost.Value);
        TrackStock = input.TrackStock && InventoryItemId is not null;
        WarrantyDays = input.WarrantyDays;
    }

    internal void RegisterRefund(int quantity) => RefundedQuantity += quantity;
}

public sealed class SalePayment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SaleId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }

    private SalePayment() { }

    internal SalePayment(Guid saleId, SalePaymentInput input)
    {
        if (!Enum.IsDefined(input.Method)) throw new DomainException("Medio de pago inválido.");
        if (input.Amount <= 0) throw new DomainException("Cada pago debe ser mayor a 0.");
        SaleId = saleId;
        Method = input.Method;
        Amount = Money.Round(input.Amount);
        Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim()[..Math.Min(input.Reference.Trim().Length, 120)];
    }
}

public sealed class SaleRefund
{
    private readonly List<SaleRefundLine> _lines = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SaleId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public bool Restocked { get; private set; }
    public string? Reason { get; private set; }
    public Guid? CashSessionId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<SaleRefundLine> Lines => _lines;

    private SaleRefund() { }

    internal SaleRefund(Guid saleId, PaymentMethod method, bool restock, string? reason, Guid userId, Guid? cashSessionId, DateTime nowUtc)
    {
        if (!Enum.IsDefined(method)) throw new DomainException("Medio de devolución inválido.");
        SaleId = saleId;
        Method = method;
        Restocked = restock;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 300)];
        CashSessionId = cashSessionId;
        CreatedByUserId = userId;
        CreatedAtUtc = nowUtc;
    }

    internal void AddLine(Guid saleLineId, int quantity, decimal amount)
    {
        _lines.Add(new SaleRefundLine(Id, saleLineId, quantity, amount));
        Amount = Money.Round(_lines.Sum(l => l.Amount));
    }

    internal void AdjustAmount(decimal amount) => Amount = Money.Round(Math.Max(0, amount));
}

public sealed class SaleRefundLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SaleRefundId { get; private set; }
    public Guid SaleLineId { get; private set; }
    public int Quantity { get; private set; }
    public decimal Amount { get; private set; }

    private SaleRefundLine() { }

    internal SaleRefundLine(Guid refundId, Guid saleLineId, int quantity, decimal amount)
    {
        SaleRefundId = refundId;
        SaleLineId = saleLineId;
        Quantity = quantity;
        Amount = amount;
    }
}
