using RepairShop.Domain.Common;

namespace RepairShop.Domain.RepairOrders;

public sealed class WorkshopWorkflow
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public long Number { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string DeviceLabel { get; set; } = "";
    public string? Identifier { get; set; }
    public string Condition { get; set; } = "";
    public string Accessories { get; set; } = "";
    public string Priority { get; set; } = "Normal";
    public string Diagnosis { get; set; } = "";
    public string IntakeChecksJson { get; set; } = "{}";
    public string QualityChecksJson { get; set; } = "{}";
    public string? PortalTokenHash { get; set; }
    public DateTime? PortalExpiresAtUtc { get; set; }
    public string? DeliveredTo { get; set; }
    public DateTime? HandedOverAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public decimal LaborCost { get; set; }
    public bool IsDemo { get; set; }
}

public sealed record QuoteItem(string Description, int Quantity, decimal UnitPrice, decimal UnitCost);

public sealed class WorkflowQuote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public Guid OrderId { get; set; }
    public int Revision { get; set; }
    public string Currency { get; set; } = "ARS";
    public string LinesJson { get; set; } = "[]";
    public decimal Total { get; set; }
    public string Terms { get; set; } = "";
    public int WarrantyDays { get; set; }
    public string Status { get; set; } = "Sent";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionBy { get; set; }

    public static decimal CalculateTotal(IReadOnlyList<QuoteItem> lines)
    {
        if (lines.Count is < 1 or > 30) throw new DomainException("Agregá entre 1 y 30 conceptos al presupuesto.");
        if (lines.Any(l => string.IsNullOrWhiteSpace(l.Description) || l.Description.Length > 200 || l.Quantity is < 1 or > 1000 || l.UnitPrice < 0 || l.UnitCost < 0 || l.UnitPrice > 100000000 || l.UnitCost > 100000000))
            throw new DomainException("Revisá descripción, cantidad y precios de cada concepto.");
        return lines.Sum(l => l.Quantity * decimal.Round(l.UnitPrice, 2, MidpointRounding.AwayFromZero));
    }

    public void Decide(bool accept, string name, DateTime now)
    {
        if (Status != "Sent") throw new DomainException("Este presupuesto ya fue respondido o reemplazado.");
        if (ExpiresAtUtc <= now) throw new DomainException("El presupuesto venció. Pedí una nueva versión al taller.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 3 or > 120) throw new DomainException("Ingresá el nombre de quien confirma.");
        Status = accept ? "Accepted" : "Rejected";
        DecisionBy = name.Trim();
        DecidedAtUtc = now;
    }
}

public sealed class WorkflowRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public string Operation { get; set; } = "";
    public string Key { get; set; } = "";
    public string Hash { get; set; } = "";
    public string ResponseJson { get; set; } = "{}";
}

public sealed class WorkflowPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public Guid OrderId { get; set; }
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "image/jpeg";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class WorkflowRefund
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public Guid OrderId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
