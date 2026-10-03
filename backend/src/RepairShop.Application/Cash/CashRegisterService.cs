using System.Text.Json;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Cash;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Cash;

/// <summary>
/// Cash register shifts: open with a float, record movements (sales, order payments, income, expenses,
/// withdrawals, refunds) and close with a count per method and currency (arqueo).
/// </summary>
public sealed class CashRegisterService
{
    private const string EntityType = "cash_session";

    private readonly ICashSessionRepository _sessions;
    private readonly ICashMovementRepository _movements;
    private readonly IShopRepository _shops;
    private readonly IUserRepository _users;
    private readonly ICounterService _counters;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public CashRegisterService(
        ICashSessionRepository sessions,
        ICashMovementRepository movements,
        IShopRepository shops,
        IUserRepository users,
        ICounterService counters,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _sessions = sessions;
        _movements = movements;
        _shops = shops;
        _users = users;
        _counters = counters;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<CashSessionResponse?> GetCurrentAsync(Guid shopId, CancellationToken ct)
    {
        var session = await _sessions.GetOpenAsync(shopId, ct);
        return session is null ? null : await ToResponseAsync(shopId, session, includeMovements: true, ct);
    }

    public async Task<CashSessionResponse> GetAsync(Guid shopId, Guid id, CancellationToken ct)
    {
        var session = await _sessions.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Caja no encontrada.");
        return await ToResponseAsync(shopId, session, includeMovements: true, ct);
    }

    public async Task<PagedResult<CashSessionResponse>> ListAsync(Guid shopId, int skip, int take, CancellationToken ct)
    {
        var (items, total) = await _sessions.SearchAsync(shopId, skip, take, ct);
        var list = new List<CashSessionResponse>();
        foreach (var s in items) list.Add(await ToResponseAsync(shopId, s, includeMovements: false, ct));
        return new PagedResult<CashSessionResponse>(list, total);
    }

    public async Task<CashSessionResponse> OpenAsync(Guid shopId, OpenCashSessionRequest req, Actor actor, CancellationToken ct)
    {
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        if (await _sessions.GetOpenAsync(shopId, ct) is not null) throw new ConflictException("Ya hay una caja abierta en esta sucursal.");

        var session = await _uow.InTransactionAsync(async c =>
        {
            var s = new CashRegisterSession(shopId, await _counters.NextAsync(shopId, CounterKeys.CashSession, c),
                string.IsNullOrWhiteSpace(req.Currency) ? shop.DefaultCurrency : req.Currency, req.OpeningCash, req.Notes, actor.UserId, _clock.UtcNow);
            await _sessions.AddAsync(s, c);
            await _audit.AddAsync(shopId, EntityType, s.Id, "cash_opened", actor, new { s.Number, s.OpeningCash, s.Currency }, c);
            await _uow.SaveChangesAsync(c);
            return s;
        }, ct);

        return await ToResponseAsync(shopId, session, includeMovements: true, ct);
    }

    public async Task<CashSessionResponse> CloseAsync(Guid shopId, CloseCashSessionRequest req, Actor actor, CancellationToken ct)
    {
        var session = await _sessions.GetOpenAsync(shopId, ct) ?? throw new DomainException("No hay una caja abierta.");
        var movements = await _movements.ListBySessionAsync(shopId, session.Id, ct);
        var summary = BuildSummary(session, movements, req.CountedCash, req.Declared);

        var expectedCash = summary.Where(s => s.Currency == session.Currency && s.Method == PaymentMethod.Cash.ToString()).Sum(s => s.Expected);
        session.Close(expectedCash, req.CountedCash, JsonSerializer.Serialize(summary), req.Notes, actor.UserId, _clock.UtcNow);

        await _audit.AddAsync(shopId, EntityType, session.Id, "cash_closed", actor,
            new { session.Number, session.ExpectedCash, session.CountedCash, session.Difference }, ct);
        await _uow.SaveChangesAsync(ct);
        return await ToResponseAsync(shopId, session, includeMovements: true, ct);
    }

    public async Task<CashMovementResponse> AddManualMovementAsync(Guid shopId, CreateCashMovementRequest req, Actor actor, CancellationToken ct)
    {
        if (req.Type is CashMovementType.Sale or CashMovementType.OrderPayment or CashMovementType.Refund)
            throw new DomainException("Ese tipo de movimiento se registra automáticamente desde ventas y pagos.");

        var session = await _sessions.GetOpenAsync(shopId, ct) ?? throw new DomainException("Abrí la caja antes de registrar movimientos.");
        var movement = new CashMovement(shopId, session.Id, req.Type, req.Method, req.Amount,
            string.IsNullOrWhiteSpace(req.Currency) ? session.Currency : req.Currency, req.Description, req.Category, null, null, actor.UserId, _clock.UtcNow);
        await _movements.AddAsync(movement, ct);
        await _audit.AddAsync(shopId, EntityType, session.Id, "cash_movement_added", actor,
            new { type = req.Type.ToString(), movement.Amount, movement.Currency, req.Description }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(movement, await NameAsync(actor.UserId, ct));
    }

    /// <summary>
    /// Used by payments and sales: returns the open session, or throws if the shop requires one and none is open.
    /// </summary>
    public async Task<CashRegisterSession?> GetSessionForPaymentAsync(Guid shopId, PaymentMethod method, bool forceOptional, CancellationToken ct)
    {
        var session = await _sessions.GetOpenAsync(shopId, ct);
        if (session is not null || forceOptional) return session;

        var shop = await _shops.GetByIdAsync(shopId, ct);
        if (shop?.RequireOpenCashSession == true)
            throw new DomainException("Abrí la caja antes de registrar cobros.");
        return null;
    }

    /// <summary>Adds a movement to the session (no save: the caller saves with its own changes).</summary>
    public Task RecordAsync(CashRegisterSession? session, Guid shopId, CashMovementType type, PaymentMethod method, decimal amount, string currency,
        string description, string relatedEntityType, Guid relatedEntityId, Actor actor, CancellationToken ct)
    {
        if (session is null || amount <= 0) return Task.CompletedTask;
        session.EnsureOpen();
        return _movements.AddAsync(new CashMovement(shopId, session.Id, type, method, amount, currency, description, null,
            relatedEntityType, relatedEntityId, actor.UserId, _clock.UtcNow), ct);
    }

    public static IReadOnlyList<CashSummaryLine> BuildSummary(CashRegisterSession session, IReadOnlyList<CashMovement> movements, decimal? countedCash, IReadOnlyList<DeclaredAmount>? declared)
    {
        var groups = movements
            .GroupBy(m => (m.Currency, m.Method))
            .ToDictionary(g => g.Key, g => (In: g.Where(x => x.IsInflow).Sum(x => x.Amount), Out: g.Where(x => !x.IsInflow).Sum(x => x.Amount)));

        // The cash line of the session currency always exists (it carries the opening float).
        groups.TryAdd((session.Currency, PaymentMethod.Cash), (0m, 0m));

        var lines = new List<CashSummaryLine>();
        foreach (var ((currency, method), (inflow, outflow)) in groups.OrderBy(g => g.Key.Currency).ThenBy(g => g.Key.Method))
        {
            var net = Money.Round(inflow - outflow);
            var opening = method == PaymentMethod.Cash && currency == session.Currency ? session.OpeningCash : 0m;
            var expected = Money.Round(opening + net);

            decimal? declaredAmount = declared?.FirstOrDefault(d => d.Method == method && string.Equals(d.Currency, currency, StringComparison.OrdinalIgnoreCase))?.Amount;
            if (method == PaymentMethod.Cash && currency == session.Currency && countedCash is not null) declaredAmount = countedCash;

            lines.Add(new CashSummaryLine(currency, method.ToString(), Money.Round(inflow), Money.Round(outflow), net, expected,
                declaredAmount, declaredAmount is null ? null : Money.Round(declaredAmount.Value - expected)));
        }

        return lines;
    }

    private async Task<CashSessionResponse> ToResponseAsync(Guid shopId, CashRegisterSession s, bool includeMovements, CancellationToken ct)
    {
        var movements = await _movements.ListBySessionAsync(shopId, s.Id, ct);
        var names = (await _users.GetByIdsAsync(movements.Select(m => m.CreatedByUserId).Append(s.OpenedByUserId)
                .Concat(s.ClosedByUserId is null ? Array.Empty<Guid>() : new[] { s.ClosedByUserId.Value }).Distinct().ToList(), ct))
            .ToDictionary(u => u.Id, u => u.DisplayName);

        IReadOnlyList<CashSummaryLine> summary;
        if (s.ClosingSummaryJson is not null)
            summary = JsonSerializer.Deserialize<List<CashSummaryLine>>(s.ClosingSummaryJson) ?? new List<CashSummaryLine>();
        else
            summary = BuildSummary(s, movements, null, null);

        return new CashSessionResponse(s.Id, s.Number, s.Status.ToString(), s.Currency, s.OpeningCash, s.OpenedByUserId, names.GetValueOrDefault(s.OpenedByUserId),
            s.OpenedAtUtc, s.OpeningNotes, s.ClosedByUserId, s.ClosedByUserId is null ? null : names.GetValueOrDefault(s.ClosedByUserId.Value), s.ClosedAtUtc,
            s.ExpectedCash, s.CountedCash, s.Difference, s.ClosingNotes, summary,
            includeMovements ? movements.Select(m => ToResponse(m, names.GetValueOrDefault(m.CreatedByUserId))).ToList() : null);
    }

    private async Task<string?> NameAsync(Guid userId, CancellationToken ct) => (await _users.GetByIdAsync(userId, ct))?.DisplayName;

    private static CashMovementResponse ToResponse(CashMovement m, string? name)
        => new(m.Id, m.Type.ToString(), m.Method.ToString(), m.Amount, m.SignedAmount, m.Currency, m.Description, m.Category,
            m.RelatedEntityType, m.RelatedEntityId, m.CreatedByUserId, name, m.CreatedAtUtc);
}
