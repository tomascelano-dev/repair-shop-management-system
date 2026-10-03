using RepairShop.Domain.Common;

namespace RepairShop.Domain.Cash;

public enum CashSessionStatus
{
    Open = 0,
    Closed = 1
}

/// <summary>
/// A cash register shift ("caja"): opened with a float, accumulates movements, closed with a count (arqueo).
/// Only one session can be open per shop.
/// </summary>
public sealed class CashRegisterSession : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }
    public int Number { get; private set; }
    public CashSessionStatus Status { get; private set; } = CashSessionStatus.Open;

    public string Currency { get; private set; } = null!;
    public decimal OpeningCash { get; private set; }

    public Guid OpenedByUserId { get; private set; }
    public DateTime OpenedAtUtc { get; private set; }
    public string? OpeningNotes { get; private set; }

    public Guid? ClosedByUserId { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public decimal? CountedCash { get; private set; }
    public decimal? ExpectedCash { get; private set; }
    public decimal? Difference { get; private set; }
    public string? ClosingNotes { get; private set; }

    // JSON snapshot of the close (expected/declared per currency and method).
    public string? ClosingSummaryJson { get; private set; }

    private CashRegisterSession() { }

    public CashRegisterSession(Guid shopId, int number, string currency, decimal openingCash, string? notes, Guid userId, DateTime nowUtc)
    {
        if (number <= 0) throw new DomainException("Número de caja inválido.");
        if (openingCash < 0) throw new DomainException("El fondo inicial no puede ser negativo.");
        if (userId == Guid.Empty) throw new DomainException("La caja debe tener un usuario.");

        ShopId = shopId;
        Number = number;
        Currency = Money.NormalizeCurrency(currency);
        OpeningCash = Money.Round(openingCash);
        OpeningNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 500)];
        OpenedByUserId = userId;
        OpenedAtUtc = nowUtc;
    }

    public bool IsOpen => Status == CashSessionStatus.Open;

    public void EnsureOpen()
    {
        if (!IsOpen) throw new DomainException("La caja está cerrada.");
    }

    public void Close(decimal expectedCash, decimal countedCash, string summaryJson, string? notes, Guid userId, DateTime nowUtc)
    {
        EnsureOpen();
        if (countedCash < 0) throw new DomainException("El efectivo contado no puede ser negativo.");
        if (userId == Guid.Empty) throw new DomainException("El cierre debe tener un usuario.");

        Status = CashSessionStatus.Closed;
        ExpectedCash = Money.Round(expectedCash);
        CountedCash = Money.Round(countedCash);
        Difference = Money.Round(countedCash - expectedCash);
        ClosingSummaryJson = summaryJson;
        ClosingNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 1000)];
        ClosedByUserId = userId;
        ClosedAtUtc = nowUtc;
    }
}
