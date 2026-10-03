using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Common;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Fiscal;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Sales;
using RepairShop.Domain.Shops;

namespace RepairShop.Application.Fiscal;

/// <summary>
/// Electronic invoices (ARCA, ex AFIP) for counter sales and repair orders, plus credit notes.
/// Letter: Responsable Inscripto => A (customer RI with CUIT) or B; Monotributo/Exento => C.
/// </summary>
public sealed class FiscalService
{
    public const string SourceSale = "sale";
    public const string SourceOrder = RepairOrderService.EntityType;
    public const decimal VatRate = 0.21m;

    private readonly IFiscalInvoiceRepository _invoices;
    private readonly IShopRepository _shops;
    private readonly IShopIntegrationRepository _integrations;
    private readonly ICustomerRepository _customers;
    private readonly ISaleRepository _sales;
    private readonly IRepairOrderRepository _orders;
    private readonly IRepairOrderReadModel _readModel;
    private readonly IFiscalAuthority _authority;
    private readonly ISecretProtector _protector;
    private readonly IAuditLog _audit;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public FiscalService(
        IFiscalInvoiceRepository invoices,
        IShopRepository shops,
        IShopIntegrationRepository integrations,
        ICustomerRepository customers,
        ISaleRepository sales,
        IRepairOrderRepository orders,
        IRepairOrderReadModel readModel,
        IFiscalAuthority authority,
        ISecretProtector protector,
        IAuditLog audit,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _invoices = invoices;
        _shops = shops;
        _integrations = integrations;
        _customers = customers;
        _sales = sales;
        _orders = orders;
        _readModel = readModel;
        _authority = authority;
        _protector = protector;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PagedResult<FiscalInvoiceResponse>> SearchAsync(Guid shopId, DateTime? from, DateTime? to, int skip, int take, CancellationToken ct)
    {
        var (items, total) = await _invoices.SearchAsync(shopId, from, to, skip, take, ct);
        return new PagedResult<FiscalInvoiceResponse>(items.Select(ToResponse).ToList(), total);
    }

    public async Task<List<FiscalInvoiceResponse>> ListBySourceAsync(Guid shopId, string sourceType, Guid sourceId, CancellationToken ct)
        => (await _invoices.ListBySourceAsync(shopId, sourceType, sourceId, ct)).Select(ToResponse).ToList();

    public async Task<FiscalInvoiceResponse> GetAsync(Guid shopId, Guid id, CancellationToken ct)
        => ToResponse(await _invoices.GetByIdAsync(shopId, id, ct) ?? throw new NotFoundException("Factura no encontrada."));

    public async Task<FiscalInvoiceResponse> IssueForSaleAsync(Guid shopId, Guid saleId, IssueInvoiceRequest req, Actor actor, CancellationToken ct)
    {
        var sale = await _sales.GetByIdAsync(shopId, saleId, ct) ?? throw new NotFoundException("Venta no encontrada.");
        if (sale.Status is SaleStatus.Voided or SaleStatus.Refunded) throw new DomainException("La venta fue anulada o devuelta.");
        if (sale.Currency != "ARS") throw new DomainException("La facturación electrónica está soportada solo en pesos (ARS).");
        await EnsureNotInvoicedAsync(shopId, SourceSale, saleId, ct);

        var customer = sale.CustomerId is null ? null : await _customers.GetByIdAsync(shopId, sale.CustomerId.Value, ct);
        var hasServices = sale.Lines.Any(l => !l.TrackStock);
        var hasProducts = sale.Lines.Any(l => l.TrackStock);
        var concept = hasServices && hasProducts ? 3 : hasServices ? 2 : 1;
        return await IssueAsync(shopId, SourceSale, saleId, sale.Total - sale.RefundedAmount, concept, customer, req, null, actor, ct);
    }

    public async Task<FiscalInvoiceResponse> IssueForOrderAsync(Guid shopId, Guid orderId, IssueInvoiceRequest req, Actor actor, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(shopId, orderId, ct) ?? throw new NotFoundException("Orden no encontrada.");
        if (order.Status == RepairOrderStatus.Cancelled) throw new DomainException("La orden está cancelada.");
        await EnsureNotInvoicedAsync(shopId, SourceOrder, orderId, ct);

        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var info = (await _readModel.GetInfoAsync(shopId, new[] { orderId }, ct))[orderId];
        var money = OrderMapping.Financials(order, info, shop.DefaultCurrency);
        if (money.Currency != "ARS") throw new DomainException("La facturación electrónica está soportada solo en pesos (ARS).");
        if (money.Total <= 0) throw new DomainException("La orden no tiene importe para facturar.");

        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        // Repairs are services, plus parts when charged separately.
        var concept = info.ExtraCharges > 0 ? 3 : 2;
        return await IssueAsync(shopId, SourceOrder, orderId, money.Total, concept, customer, req, null, actor, ct);
    }

    /// <summary>Credit note cancelling an authorized invoice (e.g. after a refund or a mistake).</summary>
    public async Task<FiscalInvoiceResponse> CreditNoteAsync(Guid shopId, Guid invoiceId, Actor actor, CancellationToken ct)
    {
        var original = await _invoices.GetByIdAsync(shopId, invoiceId, ct) ?? throw new NotFoundException("Factura no encontrada.");
        if (original.Status != FiscalInvoiceStatus.Authorized || original.IsCreditNote) throw new DomainException("Solo se puede anular una factura autorizada.");
        var existing = await _invoices.ListBySourceAsync(shopId, original.SourceType, original.SourceId, ct);
        if (existing.Any(i => i.IsCreditNote && i.AssociatedInvoiceId == original.Id && i.Status == FiscalInvoiceStatus.Authorized))
            throw new ConflictException("La factura ya tiene una nota de crédito.");

        var customer = new IssueInvoiceRequest(original.ReceiverDocumentType, original.ReceiverDocumentNumber, original.ReceiverName, original.ReceiverTaxCondition);
        return await IssueAsync(shopId, original.SourceType, original.SourceId, original.Total, original.Concept, null, customer, original, actor, ct);
    }

    private async Task<FiscalInvoiceResponse> IssueAsync(Guid shopId, string sourceType, Guid sourceId, decimal total, int concept, Customer? customer,
        IssueInvoiceRequest req, FiscalInvoice? cancels, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var shop = await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");
        var integration = await _integrations.GetAsync(shopId, ct);
        if (integration is not { FiscalEnabled: true, FiscalCertificateProtected: not null } || string.IsNullOrWhiteSpace(shop.TaxId))
            throw new DomainException("La facturación electrónica no está configurada (CUIT, punto de venta y certificado).");

        var docType = req.DocumentType ?? customer?.DocumentType ?? CustomerDocumentType.None;
        var docNumber = PhoneNumber.Digits(req.DocumentNumber ?? customer?.DocumentNumber);
        var receiverCondition = req.ReceiverTaxCondition ?? customer?.TaxCondition ?? CustomerTaxCondition.ConsumidorFinal;
        var receiverName = req.ReceiverName ?? customer?.FullName ?? "Consumidor Final";
        if (docType == CustomerDocumentType.None) docNumber = "";

        var invoiceType = cancels is not null ? CreditNoteFor(cancels.VoucherType) : ResolveType(shop.TaxCondition, receiverCondition, docType);
        if (invoiceType is FiscalVoucherType.FacturaA && docType != CustomerDocumentType.Cuit)
            throw new DomainException("Para Factura A el cliente debe tener CUIT.");

        total = Money.Round(total);
        var (net, vat) = IsLetterC(invoiceType) ? (total, 0m) : SplitVat(total);
        var invoice = new FiscalInvoice(shopId, sourceType, sourceId, invoiceType, integration.FiscalPointOfSale, concept, docType,
            docNumber.Length == 0 ? null : docNumber, receiverName, receiverCondition, net, vat, total, cancels?.Id, actor.UserId, now);

        var tz = RenderOrderMessageService.ResolveTimeZone(shop.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, tz));
        var request = new FiscalVoucherRequest(
            invoiceType, concept, ArcaDocType(docType), docNumber.Length == 0 ? 0 : long.Parse(docNumber), ArcaIvaCondition(receiverCondition),
            today, net, vat, total,
            vat > 0 ? new[] { new FiscalVatLine(5, net, vat) } : Array.Empty<FiscalVatLine>(),
            concept == 1 ? null : today, concept == 1 ? null : today, concept == 1 ? null : today,
            cancels?.VoucherType, cancels?.PointOfSale, cancels?.Number);

        var credentials = new FiscalCredentials(shop.TaxId!, integration.FiscalPointOfSale, integration.FiscalEnvironment,
            Convert.FromBase64String(_protector.Unprotect(integration.FiscalCertificateProtected!)),
            integration.FiscalCertificatePasswordProtected is null ? null : _protector.Unprotect(integration.FiscalCertificatePasswordProtected));

        try
        {
            var result = await _authority.AuthorizeAsync(credentials, request, ct);
            if (result.Approved && result.Cae is not null && result.CaeDueDate is not null)
                invoice.MarkAuthorized(result.Number, result.Cae, result.CaeDueDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), result.Message, now);
            else
                invoice.MarkRejected(result.Message, now);
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            invoice.MarkError($"No se pudo comunicar con ARCA: {ex.Message}", now);
        }

        await _invoices.AddAsync(invoice, ct);
        await _audit.AddAsync(shopId, sourceType, sourceId, cancels is null ? "invoice_issued" : "credit_note_issued", actor,
            new { invoice.Id, type = invoiceType.ToString(), status = invoice.Status.ToString(), invoice.Total, invoice.Cae }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(invoice);
    }

    private async Task EnsureNotInvoicedAsync(Guid shopId, string sourceType, Guid sourceId, CancellationToken ct)
    {
        var existing = await _invoices.ListBySourceAsync(shopId, sourceType, sourceId, ct);
        var active = existing.Where(i => !i.IsCreditNote && i.Status == FiscalInvoiceStatus.Authorized)
            .Any(i => !existing.Any(nc => nc.IsCreditNote && nc.AssociatedInvoiceId == i.Id && nc.Status == FiscalInvoiceStatus.Authorized));
        if (active) throw new ConflictException("Ya existe una factura autorizada para este comprobante.");
    }

    public static (decimal Net, decimal Vat) SplitVat(decimal total)
    {
        var net = Money.Round(total / (1 + VatRate));
        return (net, Money.Round(total - net));
    }

    public static FiscalVoucherType ResolveType(CustomerTaxCondition emitter, CustomerTaxCondition receiver, CustomerDocumentType docType)
        => emitter == CustomerTaxCondition.ResponsableInscripto
            ? receiver == CustomerTaxCondition.ResponsableInscripto && docType == CustomerDocumentType.Cuit ? FiscalVoucherType.FacturaA : FiscalVoucherType.FacturaB
            : FiscalVoucherType.FacturaC;

    public static FiscalVoucherType CreditNoteFor(FiscalVoucherType t) => t switch
    {
        FiscalVoucherType.FacturaA => FiscalVoucherType.NotaCreditoA,
        FiscalVoucherType.FacturaB => FiscalVoucherType.NotaCreditoB,
        FiscalVoucherType.FacturaC => FiscalVoucherType.NotaCreditoC,
        _ => throw new DomainException("Comprobante no soportado para nota de crédito.")
    };

    public static bool IsLetterC(FiscalVoucherType t) => t is FiscalVoucherType.FacturaC or FiscalVoucherType.NotaCreditoC or FiscalVoucherType.NotaDebitoC;

    public static string Letter(FiscalVoucherType t) => t switch
    {
        FiscalVoucherType.FacturaA or FiscalVoucherType.NotaCreditoA or FiscalVoucherType.NotaDebitoA => "A",
        FiscalVoucherType.FacturaB or FiscalVoucherType.NotaCreditoB or FiscalVoucherType.NotaDebitoB => "B",
        _ => "C"
    };

    // ARCA document type codes.
    public static int ArcaDocType(CustomerDocumentType t) => t switch
    {
        CustomerDocumentType.Cuit => 80,
        CustomerDocumentType.Cuil => 86,
        CustomerDocumentType.Passport => 94,
        CustomerDocumentType.Dni => 96,
        _ => 99
    };

    // ARCA receiver VAT condition ids (RG 5616/2024).
    public static int ArcaIvaCondition(CustomerTaxCondition c) => c switch
    {
        CustomerTaxCondition.ResponsableInscripto => 1,
        CustomerTaxCondition.Exento => 4,
        CustomerTaxCondition.Monotributo => 6,
        _ => 5
    };

    public static FiscalInvoiceResponse ToResponse(FiscalInvoice i)
        => new(i.Id, i.SourceType, i.SourceId, i.VoucherType.ToString(), Letter(i.VoucherType), i.PointOfSale, i.Number, i.Code, i.IssueDateUtc,
            i.ReceiverName, i.ReceiverDocumentNumber, i.NetAmount, i.VatAmount, i.Total, i.Status.ToString(), i.Cae, i.CaeDueDate, i.ResultMessage, i.AssociatedInvoiceId);
}
