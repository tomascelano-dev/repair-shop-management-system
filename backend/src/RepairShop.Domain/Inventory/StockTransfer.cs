using RepairShop.Domain.Common;

namespace RepairShop.Domain.Inventory;

public enum StockTransferStatus
{
    InTransit = 0,
    Received = 1,
    Cancelled = 2
}

/// <summary>
/// Moves stock between two branches of the same organization.
/// Stock leaves the origin when the transfer is created and enters the destination when received.
/// </summary>
public sealed class StockTransfer
{
    private readonly List<StockTransferLine> _lines = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public int Number { get; private set; }
    public Guid FromShopId { get; private set; }
    public Guid ToShopId { get; private set; }
    public StockTransferStatus Status { get; private set; } = StockTransferStatus.InTransit;
    public string? Notes { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? ReceivedByUserId { get; private set; }
    public DateTime? ReceivedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<StockTransferLine> Lines => _lines;

    private StockTransfer() { }

    public StockTransfer(Guid organizationId, int number, Guid fromShopId, Guid toShopId, string? notes, Guid userId, DateTime nowUtc)
    {
        if (fromShopId == Guid.Empty || toShopId == Guid.Empty) throw new DomainException("Las sucursales de la transferencia son obligatorias.");
        if (fromShopId == toShopId) throw new DomainException("La sucursal de origen y destino deben ser distintas.");
        if (userId == Guid.Empty) throw new DomainException("La transferencia debe tener un usuario.");

        OrganizationId = organizationId;
        Number = number;
        FromShopId = fromShopId;
        ToShopId = toShopId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByUserId = userId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public string Code => $"TR-{Number:D5}";

    public void AddLine(Guid sourceItemId, string sku, string name, int quantity, decimal? unitCost, string? currency)
    {
        if (Status != StockTransferStatus.InTransit) throw new DomainException("La transferencia está cerrada.");
        if (_lines.Any(l => l.SourceItemId == sourceItemId)) throw new DomainException("El ítem ya está en la transferencia.");
        _lines.Add(new StockTransferLine(Id, sourceItemId, sku, name, quantity, unitCost, currency));
    }

    public void MarkReceived(Guid userId, DateTime nowUtc)
    {
        if (Status != StockTransferStatus.InTransit) throw new DomainException("La transferencia ya fue cerrada.");
        Status = StockTransferStatus.Received;
        ReceivedByUserId = userId;
        ReceivedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status != StockTransferStatus.InTransit) throw new DomainException("La transferencia ya fue cerrada.");
        Status = StockTransferStatus.Cancelled;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed class StockTransferLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid StockTransferId { get; private set; }
    public Guid SourceItemId { get; private set; }
    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal? UnitCost { get; private set; }
    public string? UnitCostCurrency { get; private set; }

    private StockTransferLine() { }

    internal StockTransferLine(Guid transferId, Guid sourceItemId, string sku, string name, int quantity, decimal? unitCost, string? currency)
    {
        if (quantity <= 0) throw new DomainException("La cantidad a transferir debe ser mayor a 0.");
        StockTransferId = transferId;
        SourceItemId = sourceItemId;
        Sku = sku;
        Name = name;
        Quantity = quantity;
        UnitCost = unitCost;
        UnitCostCurrency = currency;
    }
}
