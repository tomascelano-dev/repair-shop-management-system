using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Ordered = 1,
    PartiallyReceived = 2,
    Received = 3,
    Cancelled = 4
}

public sealed class PurchaseOrder : IShopScoped
{
    private readonly List<PurchaseOrderLine> _lines = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public int Number { get; private set; }
    public Guid SupplierId { get; private set; }

    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;
    public string Currency { get; private set; } = null!;
    public string? Notes { get; private set; }
    public DateTime? ExpectedAtUtc { get; private set; }

    public decimal Total { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? OrderedAtUtc { get; private set; }
    public DateTime? ReceivedAtUtc { get; private set; }

    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;

    private PurchaseOrder() { }

    public PurchaseOrder(Guid shopId, int number, Guid supplierId, string currency, string? notes, DateTime? expectedAtUtc, Guid userId, DateTime nowUtc)
    {
        if (number <= 0) throw new DomainException("Número de compra inválido.");
        if (supplierId == Guid.Empty) throw new DomainException("La compra debe tener un proveedor.");
        if (userId == Guid.Empty) throw new DomainException("La compra debe tener un usuario.");

        ShopId = shopId;
        Number = number;
        SupplierId = supplierId;
        Currency = Money.NormalizeCurrency(currency);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ExpectedAtUtc = expectedAtUtc;
        CreatedByUserId = userId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public string Code => $"OC-{Number:D5}";

    public void SetLines(IEnumerable<(Guid InventoryItemId, string Description, int Quantity, decimal UnitCost)> lines, DateTime nowUtc)
    {
        if (Status != PurchaseOrderStatus.Draft) throw new DomainException("Solo se pueden editar compras en borrador.");
        var list = lines.ToList();
        if (list.Count == 0) throw new DomainException("La compra debe tener al menos un ítem.");

        _lines.Clear();
        foreach (var l in list) _lines.Add(new PurchaseOrderLine(Id, l.InventoryItemId, l.Description, l.Quantity, l.UnitCost));
        Total = Money.Round(_lines.Sum(x => x.Quantity * x.UnitCost));
        UpdatedAtUtc = nowUtc;
    }

    public void MarkOrdered(DateTime nowUtc)
    {
        if (Status != PurchaseOrderStatus.Draft) throw new DomainException("La compra ya fue enviada al proveedor.");
        if (_lines.Count == 0) throw new DomainException("La compra debe tener al menos un ítem.");
        Status = PurchaseOrderStatus.Ordered;
        OrderedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Registers received quantities. Returns the received units per line.</summary>
    public IReadOnlyList<(PurchaseOrderLine Line, int Quantity)> Receive(IReadOnlyDictionary<Guid, int> quantitiesByLineId, DateTime nowUtc)
    {
        if (Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Received)
            throw new DomainException("La compra está cerrada.");
        if (Status == PurchaseOrderStatus.Draft) MarkOrdered(nowUtc);

        var received = new List<(PurchaseOrderLine, int)>();
        foreach (var (lineId, qty) in quantitiesByLineId)
        {
            if (qty == 0) continue;
            var line = _lines.FirstOrDefault(l => l.Id == lineId) ?? throw new DomainException("Línea de compra inexistente.");
            line.Receive(qty);
            received.Add((line, qty));
        }

        if (received.Count == 0) throw new DomainException("Indicá al menos una cantidad recibida.");

        Status = _lines.All(l => l.ReceivedQuantity >= l.Quantity) ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        if (Status == PurchaseOrderStatus.Received) ReceivedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        return received;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.PartiallyReceived)
            throw new DomainException("No se puede cancelar una compra con mercadería recibida.");
        Status = PurchaseOrderStatus.Cancelled;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed class PurchaseOrderLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public string Description { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public int ReceivedQuantity { get; private set; }

    private PurchaseOrderLine() { }

    internal PurchaseOrderLine(Guid purchaseOrderId, Guid inventoryItemId, string description, int quantity, decimal unitCost)
    {
        if (inventoryItemId == Guid.Empty) throw new DomainException("La línea debe referenciar un ítem.");
        if (quantity <= 0) throw new DomainException("La cantidad debe ser mayor a 0.");
        if (unitCost < 0) throw new DomainException("El costo no puede ser negativo.");
        description = (description ?? "").Trim();
        if (description.Length < 1) throw new DomainException("La línea necesita una descripción.");

        PurchaseOrderId = purchaseOrderId;
        InventoryItemId = inventoryItemId;
        Description = description.Length > 200 ? description[..200] : description;
        Quantity = quantity;
        UnitCost = Money.Round(unitCost);
    }

    internal void Receive(int quantity)
    {
        if (quantity < 0) throw new DomainException("La cantidad recibida no puede ser negativa.");
        if (ReceivedQuantity + quantity > Quantity) throw new DomainException($"Se recibiría más de lo pedido en \"{Description}\".");
        ReceivedQuantity += quantity;
    }
}
