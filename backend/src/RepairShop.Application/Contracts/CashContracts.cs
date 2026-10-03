using System.ComponentModel.DataAnnotations;
using RepairShop.Domain.Cash;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Contracts;

public sealed record OpenCashSessionRequest(decimal OpeningCash, string? Currency = null, string? Notes = null);

public sealed record CloseCashSessionRequest(
    [Required] decimal CountedCash,
    IReadOnlyList<DeclaredAmount>? Declared = null,
    string? Notes = null);

public sealed record DeclaredAmount(PaymentMethod Method, string Currency, decimal Amount);

public sealed record CreateCashMovementRequest(
    [Required] CashMovementType Type,
    [Required] decimal Amount,
    PaymentMethod Method = PaymentMethod.Cash,
    string? Currency = null,
    [Required, MinLength(2)] string Description = "",
    string? Category = null);

public sealed record CashMovementResponse(
    Guid Id,
    string Type,
    string Method,
    decimal Amount,
    decimal SignedAmount,
    string Currency,
    string Description,
    string? Category,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    Guid CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAtUtc);

public sealed record CashSummaryLine(string Currency, string Method, decimal Inflows, decimal Outflows, decimal Net, decimal Expected, decimal? Declared, decimal? Difference);

public sealed record CashSessionResponse(
    Guid Id,
    int Number,
    string Status,
    string Currency,
    decimal OpeningCash,
    Guid OpenedByUserId,
    string? OpenedByName,
    DateTime OpenedAtUtc,
    string? OpeningNotes,
    Guid? ClosedByUserId,
    string? ClosedByName,
    DateTime? ClosedAtUtc,
    decimal? ExpectedCash,
    decimal? CountedCash,
    decimal? Difference,
    string? ClosingNotes,
    IReadOnlyList<CashSummaryLine> Summary,
    IReadOnlyList<CashMovementResponse>? Movements);
