using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.Payments;
using RepairShop.Application.Quotes;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Common;
using RepairShop.Domain.Feedback;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;

namespace RepairShop.Application.Portal;

/// <summary>
/// Public, unauthenticated tracking page accessed with the order's capability token (link / QR).
/// Exposes only what the customer needs: no internal notes, no phone numbers, no staff names.
/// </summary>
public sealed class PublicPortalService
{
    private readonly IRepairOrderRepository _orders;
    private readonly IShopRepository _shops;
    private readonly IRepairOrderStatusHistoryRepository _history;
    private readonly IRepairOrderNoteRepository _notes;
    private readonly IQuoteRepository _quotes;
    private readonly ICustomerRepository _customers;
    private readonly IDeviceRepository _devices;
    private readonly ICustomerFeedbackRepository _feedback;
    private readonly IRepairOrderReadModel _readModel;
    private readonly QuoteService _quoteService;
    private readonly PaymentLinkService _paymentLinks;
    private readonly IFileUrlSigner _fileUrls;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public PublicPortalService(
        IRepairOrderRepository orders,
        IShopRepository shops,
        IRepairOrderStatusHistoryRepository history,
        IRepairOrderNoteRepository notes,
        IQuoteRepository quotes,
        ICustomerRepository customers,
        IDeviceRepository devices,
        ICustomerFeedbackRepository feedback,
        IRepairOrderReadModel readModel,
        QuoteService quoteService,
        PaymentLinkService paymentLinks,
        IFileUrlSigner fileUrls,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _orders = orders;
        _shops = shops;
        _history = history;
        _notes = notes;
        _quotes = quotes;
        _customers = customers;
        _devices = devices;
        _feedback = feedback;
        _readModel = readModel;
        _quoteService = quoteService;
        _paymentLinks = paymentLinks;
        _fileUrls = fileUrls;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PublicTrackingResponse> GetAsync(string token, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var order = await RequireAsync(token, ct);
        var shopId = order.ShopId;
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Seguimiento no disponible.");
        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        var device = await _devices.GetByIdAsync(shopId, order.DeviceId, ct);
        var history = await _history.ListByOrderAsync(shopId, order.Id, ct);
        var notes = await _notes.ListPublicByOrderAsync(shopId, order.Id, ct);
        var quotes = await _quotes.ListByOrderAsync(shopId, order.Id, ct);
        var info = (await _readModel.GetInfoAsync(shopId, new[] { order.Id }, ct))[order.Id];
        var money = OrderMapping.Financials(order, info, shop.DefaultCurrency);
        var feedback = await _feedback.GetByOrderAsync(shopId, order.Id, ct);

        var quote = quotes.FirstOrDefault(q => q.Status == QuoteStatus.Sent)
                    ?? quotes.FirstOrDefault(q => q.Status == QuoteStatus.Approved)
                    ?? quotes.FirstOrDefault(q => q.Status is QuoteStatus.Rejected or QuoteStatus.Expired);

        var timeline = new List<PublicTimelineItem> { new(nameof(RepairOrderStatus.Received), OrderLabels.Status(RepairOrderStatus.Received), order.CreatedAtUtc) };
        timeline.AddRange(history.OrderBy(h => h.ChangedAtUtc).Select(h => new PublicTimelineItem(h.ToStatus.ToString(), OrderLabels.Status(h.ToStatus), h.ChangedAtUtc)));

        var waDigits = shop.Phone is null ? "" : PhoneNumber.ToWhatsAppDigits(shop.Phone, shop.PhoneCountryCode);
        var canPay = money.HasAgreedPrice && money.BalanceDue > 0 && order.Status != RepairOrderStatus.Cancelled && await _paymentLinks.IsEnabledAsync(shopId, ct);

        return new PublicTrackingResponse(
            new PublicShopInfo(shop.Name, shop.Phone, waDigits.Length == 0 ? null : $"https://wa.me/{waDigits}?text={Uri.EscapeDataString($"Hola! Consulto por la orden {order.Code}")}",
                shop.AddressLine, shop.City, shop.PickupHours, shop.LogoFileId is null ? null : _fileUrls.GetUrl(shop.LogoFileId.Value, TimeSpan.FromHours(6))),
            new PublicOrderInfo(order.Code, order.Status.ToString(), OrderLabels.Status(order.Status),
                customer?.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
                device is null ? "" : $"{device.Brand} {device.Model}", order.IssueDescription, order.CreatedAtUtc, order.PromisedAtUtc,
                order.ReadyAtUtc, order.DeliveredAtUtc, order.IsWarrantyClaim),
            timeline,
            notes.Select(n => new PublicNote(n.Body, n.CreatedAtUtc)).ToList(),
            quote is null ? null : new PublicQuote(quote.Id, quote.Version, quote.Status.ToString(), OrderLabels.Quote(quote.Status), quote.Currency,
                quote.Items.OrderBy(i => i.Position).Select(i => new PublicQuoteItem(i.Description, i.Quantity, i.UnitPrice, i.LineTotal)).ToList(),
                quote.Subtotal, quote.DiscountAmount, quote.Total, quote.ValidUntilUtc, quote.WarrantyDays, quote.Notes,
                quote.Status == QuoteStatus.Sent && (quote.ValidUntilUtc is null || quote.ValidUntilUtc > now) && !order.IsFinal),
            money.HasAgreedPrice || money.Paid > 0 ? new PublicMoney(money.Currency, money.Total, money.Paid, Math.Max(0, money.BalanceDue)) : null,
            canPay,
            order.Status == RepairOrderStatus.Delivered && order.WarrantyDays > 0
                ? new PublicWarranty(order.WarrantyDays.Value, order.WarrantyExpiresAtUtc, order.IsUnderWarranty(now))
                : null,
            order.Status == RepairOrderStatus.Delivered && feedback is null,
            feedback is not null);
    }

    public async Task<PublicTrackingResponse> DecideQuoteAsync(string token, Guid quoteId, bool approve, string? note, string? ip, CancellationToken ct)
    {
        var order = await RequireAsync(token, ct);
        await _quoteService.DecideFromPortalAsync(order.ShopId, order.Id, quoteId, approve, note, ip, ct);
        return await GetAsync(token, ct);
    }

    public async Task<PublicPaymentLinkResponse> CreatePaymentLinkAsync(string token, CancellationToken ct)
    {
        var order = await RequireAsync(token, ct);
        var link = await _paymentLinks.CreateForOrderAsync(order.ShopId, order.Id, Actor.System, ct);
        return new PublicPaymentLinkResponse(link.Url, link.Amount, link.Currency);
    }

    public async Task<PublicFeedbackResponse> SubmitFeedbackAsync(string token, PublicFeedbackRequest req, CancellationToken ct)
    {
        var order = await RequireAsync(token, ct);
        if (order.Status != RepairOrderStatus.Delivered) throw new DomainException("La encuesta se habilita cuando el equipo fue entregado.");
        if (await _feedback.GetByOrderAsync(order.ShopId, order.Id, ct) is not null) throw new ConflictException("¡Gracias! Ya recibimos tu opinión.");

        var shop = await _shops.GetByIdAsync(order.ShopId, ct);
        await _feedback.AddAsync(new CustomerFeedback(order.ShopId, order.Id, order.CustomerId, req.Score, req.Comment, _clock.UtcNow), ct);
        await _audit.AddAsync(order.ShopId, RepairOrderService.EntityType, order.Id, "feedback_received", Actor.System, new { req.Score }, ct);
        await _uow.SaveChangesAsync(ct);

        return new PublicFeedbackResponse(true, req.Score >= 4 ? shop?.GoogleReviewUrl : null);
    }

    public async Task<RepairOrder> RequireAsync(string token, CancellationToken ct)
    {
        token = (token ?? "").Trim();
        if (token.Length is < 20 or > 64) throw new NotFoundException("Seguimiento no encontrado.");
        return await _orders.GetByPublicTokenAsync(token, ct) ?? throw new NotFoundException("Seguimiento no encontrado.");
    }
}
