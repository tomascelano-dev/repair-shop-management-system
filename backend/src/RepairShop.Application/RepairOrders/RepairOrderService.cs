using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Files;
using RepairShop.Application.Notifications;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Users;

namespace RepairShop.Application.RepairOrders;

public sealed class RepairOrderService
{
    public const string EntityType = "repair_order";

    private readonly IRepairOrderRepository _orders;
    private readonly ICustomerRepository _customers;
    private readonly IDeviceRepository _devices;
    private readonly IUserRepository _users;
    private readonly IUserShopAccessRepository _access;
    private readonly IShopRepository _shops;
    private readonly IRepairOrderPaymentRepository _payments;
    private readonly IRepairOrderPartUsageRepository _parts;
    private readonly IRepairOrderReadModel _readModel;
    private readonly ICounterService _counters;
    private readonly ISecretProtector _protector;
    private readonly FileService _files;
    private readonly NotificationService _notifications;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly IAppLinks _links;

    public RepairOrderService(
        IRepairOrderRepository orders,
        ICustomerRepository customers,
        IDeviceRepository devices,
        IUserRepository users,
        IUserShopAccessRepository access,
        IShopRepository shops,
        IRepairOrderPaymentRepository payments,
        IRepairOrderPartUsageRepository parts,
        IRepairOrderReadModel readModel,
        ICounterService counters,
        ISecretProtector protector,
        FileService files,
        NotificationService notifications,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IAppLinks links)
    {
        _orders = orders;
        _customers = customers;
        _devices = devices;
        _users = users;
        _access = access;
        _shops = shops;
        _payments = payments;
        _parts = parts;
        _readModel = readModel;
        _counters = counters;
        _protector = protector;
        _files = files;
        _notifications = notifications;
        _audit = audit;
        _uow = uow;
        _clock = clock;
        _links = links;
    }

    // ===== Queries =====

    public async Task<RepairOrderResponse> GetAsync(Guid shopId, Guid id, CancellationToken ct)
    {
        var order = await RequireAsync(shopId, id, ct);
        return (await ToResponsesAsync(shopId, new[] { order }, ct)).Single();
    }

    public async Task<PagedResult<RepairOrderResponse>> SearchAsync(Guid shopId, RepairOrderSearchOptions options, CancellationToken ct)
    {
        var (items, total) = await _orders.SearchAsync(shopId, options with { NowUtc = _clock.UtcNow }, ct);
        return new PagedResult<RepairOrderResponse>(await ToResponsesAsync(shopId, items, ct), total);
    }

    public async Task<OrderBoardResponse> GetBoardAsync(Guid shopId, string? q, Guid? technicianId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var statuses = new[]
        {
            RepairOrderStatus.Received, RepairOrderStatus.Diagnosing, RepairOrderStatus.WaitingParts,
            RepairOrderStatus.InProgress, RepairOrderStatus.Testing, RepairOrderStatus.Ready
        };

        var (orders, _) = await _orders.SearchAsync(shopId, new RepairOrderSearchOptions(
            Q: q, Statuses: statuses, AssignedTechnicianId: technicianId, SortBy: "priority", SortDir: "desc", Take: 200, NowUtc: now), ct);
        var info = await _readModel.GetInfoAsync(shopId, orders.Select(o => o.Id).ToList(), ct);

        var columns = statuses.Select(s =>
        {
            var cards = orders.Where(o => o.Status == s)
                .OrderByDescending(o => o.Priority).ThenBy(o => o.PromisedAtUtc ?? DateTime.MaxValue).ThenBy(o => o.CreatedAtUtc)
                .Select(o => OrderMapping.ToCard(o, info[o.Id], shop.DefaultCurrency, shop.StaleOrderDays, now))
                .ToList();
            return new OrderBoardColumn(s.ToString(), OrderLabels.Status(s), cards.Count, cards);
        }).ToList();

        return new OrderBoardResponse(columns);
    }

    public async Task<OrderFinancialsResponse> GetFinancialsAsync(Guid shopId, RepairOrder order, CancellationToken ct)
    {
        var shop = await _shops.GetByIdAsync(shopId, ct);
        var info = (await _readModel.GetInfoAsync(shopId, new[] { order.Id }, ct))[order.Id];
        return OrderMapping.Financials(order, info, shop?.DefaultCurrency ?? "ARS");
    }

    public async Task<List<RepairOrderResponse>> ToResponsesAsync(Guid shopId, IReadOnlyCollection<RepairOrder> orders, CancellationToken ct)
    {
        if (orders.Count == 0) return new List<RepairOrderResponse>();
        var shop = await _shops.GetByIdAsync(shopId, ct);
        var info = await _readModel.GetInfoAsync(shopId, orders.Select(o => o.Id).ToList(), ct);
        var now = _clock.UtcNow;
        return orders.Select(o => OrderMapping.ToResponse(o, info[o.Id], shop?.DefaultCurrency ?? "ARS", _links, now)).ToList();
    }

    // ===== Commands =====

    public async Task<RepairOrderResponse> CreateAsync(Guid shopId, RepairOrderCreateRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var customer = await _customers.GetByIdAsync(shopId, req.CustomerId, ct) ?? throw new NotFoundException("El cliente no existe en esta sucursal.");
        var device = await _devices.GetByIdAsync(shopId, req.DeviceId, ct) ?? throw new NotFoundException("El equipo no existe en esta sucursal.");
        if (device.CustomerId != customer.Id) throw new DomainException("El equipo seleccionado no pertenece a ese cliente.");
        if (req.AssignedTechnicianId is not null) await EnsureTechnicianAsync(shopId, req.AssignedTechnicianId.Value, ct);

        var order = await _uow.InTransactionAsync(async c =>
        {
            var o = new RepairOrder(shopId, customer.Id, device.Id, req.IssueDescription, req.Notes, now);
            o.AssignNumber(await _counters.NextAsync(shopId, CounterKeys.RepairOrder, c));
            o.SetCategory(req.IssueCategory, now);
            o.Plan(req.AssignedTechnicianId, req.Priority, req.PromisedAtUtc, now);
            if (req.WarrantyDays is not null) o.SetWarrantyDays(req.WarrantyDays, now);
            await _orders.AddAsync(o, c);
            await _audit.AddAsync(shopId, EntityType, o.Id, "order_created", actor, new { o.OrderNumber, customerId = customer.Id, deviceId = device.Id }, c);
            await _uow.SaveChangesAsync(c);
            return o;
        }, ct);

        if (req.SendReceivedMessage)
            await _notifications.NotifyOrderAsync(shopId, order, OrderLabels.StatusTemplateKey(RepairOrderStatus.Received),
                $"order:{order.Id}:status:Received", true, null, actor, ct);

        return await GetAsync(shopId, order.Id, ct);
    }

    public async Task<RepairOrderResponse> UpdateAsync(Guid shopId, Guid id, RepairOrderUpdateRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var order = await RequireAsync(shopId, id, ct);
        order.Update(req.IssueDescription, req.Notes, now);
        order.SetCategory(req.IssueCategory, now);
        if (req.WarrantyDays is not null) order.SetWarrantyDays(req.WarrantyDays, now);
        await _audit.AddAsync(shopId, EntityType, order.Id, "order_updated", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    public async Task<RepairOrderResponse> PlanAsync(Guid shopId, Guid id, PlanOrderRequest req, Actor actor, CancellationToken ct)
    {
        var order = await RequireAsync(shopId, id, ct);
        if (req.AssignedTechnicianId is not null && req.AssignedTechnicianId != Guid.Empty)
            await EnsureTechnicianAsync(shopId, req.AssignedTechnicianId.Value, ct);

        var previousTech = order.AssignedTechnicianId;
        order.Plan(req.AssignedTechnicianId, req.Priority, req.PromisedAtUtc, _clock.UtcNow);
        await _audit.AddAsync(shopId, EntityType, order.Id, "order_planned", actor,
            new { from = previousTech, to = order.AssignedTechnicianId, priority = req.Priority.ToString(), req.PromisedAtUtc }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    /// <summary>Manual agreed price (legacy endpoint, kept for compatibility). Prefer approving a quote.</summary>
    public async Task<RepairOrderResponse> SetAgreedPriceAsync(Guid shopId, Guid id, SetOrderQuoteRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var order = await RequireAsync(shopId, id, ct);

        if (req.Amount is null)
        {
            order.ClearQuote(actor.UserId, now);
            await _audit.AddAsync(shopId, EntityType, order.Id, "quote_cleared", actor, null, ct);
        }
        else
        {
            var currency = Money.NormalizeCurrency(req.Currency);
            await EnsureCurrencyMatchesPaymentsAsync(shopId, order, currency, ct);
            order.SetQuote(req.Amount.Value, currency, actor.UserId, now);
            await _audit.AddAsync(shopId, EntityType, order.Id, "quote_set", actor, new { amount = req.Amount, currency }, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    public async Task DeleteAsync(Guid shopId, Guid id, Actor actor, CancellationToken ct)
    {
        var order = await RequireAsync(shopId, id, ct);
        if ((await _payments.ListByOrderAsync(shopId, id, ct)).Count > 0)
            throw new ConflictException("La orden tiene pagos registrados: cancelala en lugar de eliminarla.");
        if (await _parts.CountByOrderAsync(shopId, id, ct) > 0)
            throw new ConflictException("La orden tiene repuestos consumidos: cancelala en lugar de eliminarla.");
        if (await _orders.HasWarrantyClaimsAsync(shopId, id, ct))
            throw new ConflictException("La orden tiene reingresos por garantía asociados.");

        await _orders.RemoveAsync(order, ct);
        await _audit.AddAsync(shopId, EntityType, order.Id, "order_deleted", actor, new { order.OrderNumber }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<RepairOrderResponse> CreateWarrantyClaimAsync(Guid shopId, Guid originalId, WarrantyClaimRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var original = await RequireAsync(shopId, originalId, ct);
        if (original.Status != RepairOrderStatus.Delivered) throw new DomainException("Solo se puede reclamar garantía de una orden entregada.");
        if (!original.IsUnderWarranty(now)) throw new DomainException("La garantía de esta orden está vencida.");

        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var claim = await _uow.InTransactionAsync(async c =>
        {
            var o = new RepairOrder(shopId, original.CustomerId, original.DeviceId, req.IssueDescription,
                string.IsNullOrWhiteSpace(req.Notes) ? $"Reingreso por garantía de {original.Code}" : req.Notes, now);
            o.AssignNumber(await _counters.NextAsync(shopId, CounterKeys.RepairOrder, c));
            o.MarkAsWarrantyClaim(original.Id, original.QuoteCurrency ?? shop.DefaultCurrency, now);
            o.Plan(original.AssignedTechnicianId, RepairOrderPriority.High, null, now);
            o.SetCategory(original.IssueCategory, now);
            await _orders.AddAsync(o, c);
            await _audit.AddAsync(shopId, EntityType, o.Id, "warranty_claim_created", actor, new { originalOrderId = original.Id, original = original.Code }, c);
            await _audit.AddAsync(shopId, EntityType, original.Id, "warranty_claimed", actor, new { claimOrderId = o.Id }, c);
            await _uow.SaveChangesAsync(c);
            return o;
        }, ct);

        return await GetAsync(shopId, claim.Id, ct);
    }

    public async Task<RepairOrderResponse> RegenerateTrackingTokenAsync(Guid shopId, Guid id, Actor actor, CancellationToken ct)
    {
        var order = await RequireAsync(shopId, id, ct);
        order.RegenerateToken(_clock.UtcNow);
        await _audit.AddAsync(shopId, EntityType, order.Id, "tracking_token_regenerated", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    // ===== Unlock code (encrypted, audited) =====

    public async Task SetUnlockSecretAsync(Guid shopId, Guid id, SetUnlockSecretRequest req, Actor actor, CancellationToken ct)
    {
        var order = await RequireAsync(shopId, id, ct);
        var value = (req.Value ?? "").Trim();
        if (req.Method != UnlockMethod.None)
        {
            if (value.Length is < 1 or > 200) throw new DomainException("Ingresá el código, contraseña o patrón.");
            if (req.Method == UnlockMethod.Pattern && !IsValidPattern(value))
                throw new DomainException("El patrón debe ser una secuencia de puntos 1-9 sin repetir (ej: 1-5-9-6).");
        }

        order.SetUnlockSecret(req.Method, req.Method == UnlockMethod.None ? null : _protector.Protect(value), _clock.UtcNow);
        await _audit.AddAsync(shopId, EntityType, order.Id, req.Method == UnlockMethod.None ? "unlock_secret_cleared" : "unlock_secret_set", actor,
            new { method = req.Method.ToString() }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<UnlockSecretResponse> RevealUnlockSecretAsync(Guid shopId, Guid id, Actor actor, CancellationToken ct)
    {
        var order = await RequireAsync(shopId, id, ct);
        if (order.UnlockSecretProtected is null)
            return new UnlockSecretResponse(order.UnlockMethod.ToString(), null);

        await _audit.AddAsync(shopId, EntityType, order.Id, "unlock_secret_viewed", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return new UnlockSecretResponse(order.UnlockMethod.ToString(), _protector.Unprotect(order.UnlockSecretProtected));
    }

    // ===== Signatures =====

    public async Task<RepairOrderResponse> SaveSignatureAsync(Guid shopId, Guid id, SaveSignatureRequest req, Actor actor, CancellationToken ct)
    {
        var order = await RequireAsync(shopId, id, ct);
        var file = await _files.SaveDataUrlAsync(shopId, req.ImageDataUrl, $"firma-{req.Kind.ToString().ToLowerInvariant()}-{order.OrderNumber}.png", "signature", actor.UserId, ct);
        var now = _clock.UtcNow;
        if (req.Kind == SignatureKind.Reception) order.SetReceptionSignature(file.Id, req.SignerName, now);
        else order.SetDeliverySignature(file.Id, req.SignerName, now);

        await _audit.AddAsync(shopId, EntityType, order.Id, $"signature_{req.Kind.ToString().ToLowerInvariant()}_saved", actor, new { req.SignerName }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(shopId, id, ct);
    }

    // ---------------------------------------------------------------------------------------------

    public async Task<RepairOrder> RequireAsync(Guid shopId, Guid id, CancellationToken ct)
        => await _orders.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Orden no encontrada.");

    private async Task EnsureTechnicianAsync(Guid shopId, Guid userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive) throw new DomainException("El técnico seleccionado no existe o está inactivo.");
        var role = user.ShopId == shopId ? user.Role : (await _access.GetAsync(userId, shopId, ct))?.Role;
        if (role is null) throw new DomainException("El técnico seleccionado no trabaja en esta sucursal.");
        if (role is not (UserRole.Tech or UserRole.Admin)) throw new DomainException("Solo se pueden asignar técnicos o administradores.");
    }

    private async Task EnsureCurrencyMatchesPaymentsAsync(Guid shopId, RepairOrder order, string currency, CancellationToken ct)
    {
        var payments = await _payments.ListByOrderAsync(shopId, order.Id, ct);
        if (payments.Any(p => p.Currency != currency))
            throw new DomainException($"La orden ya tiene pagos en {payments.First().Currency}: el precio debe estar en la misma moneda.");
    }

    private static bool IsValidPattern(string value)
    {
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 3
               && parts.All(p => p.Length == 1 && p[0] is >= '1' and <= '9')
               && parts.Distinct().Count() == parts.Length;
    }
}
