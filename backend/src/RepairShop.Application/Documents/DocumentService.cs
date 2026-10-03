using System.Text;
using System.Text.Json;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Cash;
using RepairShop.Application.Common;
using RepairShop.Application.Files;
using RepairShop.Application.Fiscal;
using RepairShop.Application.RepairOrders;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Fiscal;
using RepairShop.Domain.Quotes;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Shops;

namespace RepairShop.Application.Documents;

public sealed record GeneratedDocument(byte[] Content, string FileName, string ContentType = "application/pdf");

/// <summary>Collects the data of each printable document and delegates rendering to <see cref="IPdfRenderer"/>.</summary>
public sealed class DocumentService
{
    private readonly IPdfRenderer _pdf;
    private readonly IShopRepository _shops;
    private readonly IRepairOrderRepository _orders;
    private readonly ICustomerRepository _customers;
    private readonly IDeviceRepository _devices;
    private readonly IUserRepository _users;
    private readonly IRepairOrderReceptionChecklistRepository _checklists;
    private readonly IRepairOrderPaymentRepository _payments;
    private readonly IQuoteRepository _quotes;
    private readonly ISaleRepository _sales;
    private readonly IFiscalInvoiceRepository _invoices;
    private readonly ICashSessionRepository _cashSessions;
    private readonly ICashMovementRepository _cashMovements;
    private readonly IRepairOrderReadModel _readModel;
    private readonly FileService _files;
    private readonly IAppLinks _links;

    public DocumentService(
        IPdfRenderer pdf,
        IShopRepository shops,
        IRepairOrderRepository orders,
        ICustomerRepository customers,
        IDeviceRepository devices,
        IUserRepository users,
        IRepairOrderReceptionChecklistRepository checklists,
        IRepairOrderPaymentRepository payments,
        IQuoteRepository quotes,
        ISaleRepository sales,
        IFiscalInvoiceRepository invoices,
        ICashSessionRepository cashSessions,
        ICashMovementRepository cashMovements,
        IRepairOrderReadModel readModel,
        FileService files,
        IAppLinks links)
    {
        _pdf = pdf;
        _shops = shops;
        _orders = orders;
        _customers = customers;
        _devices = devices;
        _users = users;
        _checklists = checklists;
        _payments = payments;
        _quotes = quotes;
        _sales = sales;
        _invoices = invoices;
        _cashSessions = cashSessions;
        _cashMovements = cashMovements;
        _readModel = readModel;
        _files = files;
        _links = links;
    }

    public async Task<GeneratedDocument> IntakeReceiptAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        var (shop, order, tz) = await OrderContextAsync(shopId, orderId, ct);
        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        var device = await _devices.GetByIdAsync(shopId, order.DeviceId, ct);
        var checklist = await _checklists.GetByOrderAsync(shopId, orderId, ct);
        var tech = order.AssignedTechnicianId is null ? null : await _users.GetByIdAsync(order.AssignedTechnicianId.Value, ct);
        var payments = await _payments.ListByOrderAsync(shopId, orderId, ct);

        var items = new List<DocKeyValue>();
        if (checklist is not null)
        {
            string Ok(bool v) => v ? "OK" : "Con falla";
            items.Add(new("Pantalla", Ok(checklist.ScreenOk)));
            items.Add(new("Cámaras", Ok(checklist.CamerasOk)));
            items.Add(new("Parlantes", Ok(checklist.SpeakersOk)));
            items.Add(new("Micrófono", Ok(checklist.MicrophoneOk)));
            items.Add(new("Botones", Ok(checklist.ButtonsOk)));
            items.Add(new("Face ID", Ok(checklist.FaceIdOk)));
            items.Add(new("Huella", Ok(checklist.FingerprintOk)));
            items.Add(new("Bloqueo de cuenta", checklist.CloudLock switch { CloudLockStatus.On => "Activado", CloudLockStatus.Off => "Desactivado", _ => "Sin verificar" }));
            if (checklist.BatteryPercent is not null) items.Add(new("Batería", $"{checklist.BatteryPercent}%"));
        }

        var model = new IntakeReceiptModel(
            await DocShopAsync(shop, ct), order.Code, Local(order.CreatedAtUtc, tz), Party(customer), DeviceName(device), device?.SerialNumber, device?.Imei,
            order.IssueDescription, order.Notes, items, checklist?.CosmeticNotes,
            order.UnlockMethod == UnlockMethod.None ? null : $"{UnlockLabel(order.UnlockMethod)} registrado (se elimina al entregar)",
            shop.ReceptionTerms ?? DefaultReceptionTerms,
            await _files.ReadBytesAsync(shopId, order.ReceptionSignatureFileId, ct), order.ReceptionSignedByName,
            order.PromisedAtUtc is null ? null : Local(order.PromisedAtUtc.Value, tz), tech?.DisplayName,
            payments.Where(p => p.Type == PaymentType.Payment).Select(p => new DocKeyValue(p.IsDeposit ? "Seña" : "Pago", $"{Format(p.Amount)} {p.Currency}")).ToList(),
            _links.Tracking(order.PublicToken));

        return new GeneratedDocument(_pdf.IntakeReceipt(model), $"ingreso-{order.OrderNumber:D6}.pdf");
    }

    public async Task<GeneratedDocument> LabelAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        var (shop, order, tz) = await OrderContextAsync(shopId, orderId, ct);
        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        var device = await _devices.GetByIdAsync(shopId, order.DeviceId, ct);
        var model = new LabelModel(shop.Name, order.Code, customer?.FullName ?? "", DeviceName(device), Local(order.CreatedAtUtc, tz), order.IssueDescription,
            _links.Order(order.Id));
        return new GeneratedDocument(_pdf.Label(model), $"etiqueta-{order.OrderNumber:D6}.pdf");
    }

    public async Task<GeneratedDocument> QuoteAsync(Guid shopId, Guid orderId, Guid? quoteId, CancellationToken ct)
    {
        var (shop, order, tz) = await OrderContextAsync(shopId, orderId, ct);
        var quotes = await _quotes.ListByOrderAsync(shopId, orderId, ct);
        var quote = quoteId is null
            ? quotes.FirstOrDefault(q => q.Status is QuoteStatus.Sent or QuoteStatus.Approved) ?? quotes.FirstOrDefault()
            : quotes.FirstOrDefault(q => q.Id == quoteId);
        if (quote is null) throw new NotFoundException("La orden no tiene presupuesto.");

        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        var device = await _devices.GetByIdAsync(shopId, order.DeviceId, ct);
        var model = new QuoteDocumentModel(await DocShopAsync(shop, ct), order.Code, quote.Version, OrderLabels.Quote(quote.Status), Local(quote.CreatedAtUtc, tz),
            quote.ValidUntilUtc is null ? null : Local(quote.ValidUntilUtc.Value, tz), Party(customer), DeviceName(device), order.IssueDescription,
            quote.Items.OrderBy(i => i.Position).Select(i => new DocLine(i.Description, i.Quantity, i.UnitPrice, 0, i.LineTotal)).ToList(),
            quote.Subtotal, quote.DiscountAmount, quote.Total, quote.Currency, quote.WarrantyDays, quote.Notes, _links.Tracking(order.PublicToken));
        return new GeneratedDocument(_pdf.Quote(model), $"presupuesto-{order.OrderNumber:D6}-v{quote.Version}.pdf");
    }

    public async Task<GeneratedDocument> PaymentReceiptAsync(Guid shopId, Guid orderId, Guid paymentId, CancellationToken ct)
    {
        var (shop, order, tz) = await OrderContextAsync(shopId, orderId, ct);
        var payment = await _payments.GetByIdAsync(shopId, paymentId, ct);
        if (payment is null || payment.RepairOrderId != orderId) throw new NotFoundException("Pago no encontrado.");

        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        var device = await _devices.GetByIdAsync(shopId, order.DeviceId, ct);
        var info = (await _readModel.GetInfoAsync(shopId, new[] { orderId }, ct))[orderId];
        var money = OrderMapping.Financials(order, info, shop.DefaultCurrency);

        var model = new PaymentReceiptModel(await DocShopAsync(shop, ct), $"R-{order.OrderNumber:D6}-{payment.CreatedAtUtc:yyMMddHHmm}", Local(payment.CreatedAtUtc, tz),
            Party(customer), $"{(payment.Type == PaymentType.Refund ? "Devolución" : payment.IsDeposit ? "Seña" : "Pago")} de la orden {order.Code}",
            MethodLabel(payment.Method), payment.Amount, payment.Currency, payment.Type == PaymentType.Refund, money.Total, money.Paid, Math.Max(0, money.BalanceDue),
            payment.Reference, DeviceName(device));
        return new GeneratedDocument(_pdf.PaymentReceipt(model), $"recibo-{order.OrderNumber:D6}.pdf");
    }

    public async Task<GeneratedDocument> SaleTicketAsync(Guid shopId, Guid saleId, CancellationToken ct)
    {
        var shop = await RequireShopAsync(shopId, ct);
        var tz = RenderOrderMessageService.ResolveTimeZone(shop.TimeZone);
        var sale = await _sales.GetByIdAsync(shopId, saleId, ct) ?? throw new NotFoundException("Venta no encontrada.");
        var customer = sale.CustomerId is null ? null : await _customers.GetByIdAsync(shopId, sale.CustomerId.Value, ct);
        var cashier = await _users.GetByIdAsync(sale.CreatedByUserId, ct);

        var footer = sale.Lines.Where(l => l.WarrantyDays is > 0).Select(l => $"Garantía {l.WarrantyDays} días: {l.Description}").ToList();
        footer.Add("Conservá este ticket para cambios y garantías.");
        var model = new SaleTicketModel(await DocShopAsync(shop, ct), sale.Code, Local(sale.CreatedAtUtc, tz), customer?.FullName,
            sale.Lines.OrderBy(l => l.Position).Select(l => new DocLine(l.Description, l.Quantity, l.UnitPrice, l.DiscountAmount, l.LineTotal)).ToList(),
            sale.Subtotal, sale.DiscountAmount, sale.Total,
            sale.Payments.Select(p => new DocKeyValue(MethodLabel(p.Method), Format(p.Amount))).ToList(),
            sale.ChangeAmount, sale.Currency, cashier?.DisplayName,
            sale.Status switch { Domain.Sales.SaleStatus.Voided => "ANULADA", Domain.Sales.SaleStatus.Refunded => "DEVUELTA", Domain.Sales.SaleStatus.PartiallyRefunded => "DEVOLUCIÓN PARCIAL", _ => "" },
            footer);
        return new GeneratedDocument(_pdf.SaleTicket(model), $"ticket-{sale.Code}.pdf");
    }

    public async Task<GeneratedDocument> WarrantyCertificateAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        var (shop, order, tz) = await OrderContextAsync(shopId, orderId, ct);
        if (order.Status != RepairOrderStatus.Delivered || order.DeliveredAtUtc is null)
            throw new Domain.Common.DomainException("El certificado de garantía se emite cuando el equipo fue entregado.");

        var customer = await _customers.GetByIdAsync(shopId, order.CustomerId, ct);
        var device = await _devices.GetByIdAsync(shopId, order.DeviceId, ct);
        var approved = await _quotes.GetApprovedAsync(shopId, orderId, ct);
        var work = approved?.Items.OrderBy(i => i.Position).Select(i => i.WarrantyDays is null ? i.Description : $"{i.Description} (garantía {i.WarrantyDays} días)").ToList()
                   ?? new List<string> { order.IssueDescription };

        var model = new WarrantyCertificateModel(await DocShopAsync(shop, ct), order.Code, Party(customer), DeviceName(device), device?.SerialNumber ?? device?.Imei,
            order.IssueDescription, work, Local(order.DeliveredAtUtc.Value, tz), order.WarrantyDays ?? 0,
            order.WarrantyExpiresAtUtc is null ? null : Local(order.WarrantyExpiresAtUtc.Value, tz), shop.WarrantyTerms ?? DefaultWarrantyTerms,
            await _files.ReadBytesAsync(shopId, order.DeliverySignatureFileId, ct), order.DeliverySignedByName, _links.Tracking(order.PublicToken));
        return new GeneratedDocument(_pdf.WarrantyCertificate(model), $"garantia-{order.OrderNumber:D6}.pdf");
    }

    public async Task<GeneratedDocument> InvoiceAsync(Guid shopId, Guid invoiceId, CancellationToken ct)
    {
        var shop = await RequireShopAsync(shopId, ct);
        var tz = RenderOrderMessageService.ResolveTimeZone(shop.TimeZone);
        var invoice = await _invoices.GetByIdAsync(shopId, invoiceId, ct) ?? throw new NotFoundException("Factura no encontrada.");

        IReadOnlyList<DocLine> lines;
        if (invoice.SourceType == FiscalService.SourceSale)
        {
            var sale = await _sales.GetByIdAsync(shopId, invoice.SourceId, ct);
            lines = sale?.Lines.OrderBy(l => l.Position).Select(l => new DocLine(l.Description, l.Quantity, l.UnitPrice, l.DiscountAmount, l.LineTotal)).ToList()
                    ?? new List<DocLine>();
            if (sale is not null && sale.DiscountAmount > 0) lines = lines.Append(new DocLine("Descuento", 1, -sale.DiscountAmount, 0, -sale.DiscountAmount)).ToList();
        }
        else
        {
            var order = await _orders.GetByIdAsync(shopId, invoice.SourceId, ct);
            var approved = order is null ? null : await _quotes.GetApprovedAsync(shopId, order.Id, ct);
            lines = approved is not null
                ? approved.Items.OrderBy(i => i.Position).Select(i => new DocLine(i.Description, i.Quantity, i.UnitPrice, 0, i.LineTotal)).ToList()
                : new List<DocLine> { new($"Reparación orden {order?.Code}", 1, invoice.Total, 0, invoice.Total) };
        }

        var letter = FiscalService.Letter(invoice.VoucherType);
        var model = new InvoiceDocumentModel(await DocShopAsync(shop, ct), letter, invoice.IsCreditNote ? "NOTA DE CRÉDITO" : "FACTURA", (int)invoice.VoucherType,
            invoice.PointOfSale, invoice.Number, Local(invoice.IssueDateUtc, tz),
            new DocParty(invoice.ReceiverName, null, null, invoice.ReceiverDocumentNumber is null ? null : $"{invoice.ReceiverDocumentType}: {invoice.ReceiverDocumentNumber}"),
            TaxConditionLabel(invoice.ReceiverTaxCondition), lines, invoice.NetAmount, invoice.VatAmount, invoice.Total, invoice.Cae, invoice.CaeDueDate,
            invoice.Status == FiscalInvoiceStatus.Authorized ? ArcaQrUrl(invoice, shop.TaxId) : null, invoice.Status.ToString());
        return new GeneratedDocument(_pdf.Invoice(model), $"{(invoice.IsCreditNote ? "nc" : "factura")}-{letter}-{invoice.Code}.pdf");
    }

    public async Task<GeneratedDocument> CashReportAsync(Guid shopId, Guid sessionId, CancellationToken ct)
    {
        var shop = await RequireShopAsync(shopId, ct);
        var tz = RenderOrderMessageService.ResolveTimeZone(shop.TimeZone);
        var session = await _cashSessions.GetByIdAsync(shopId, sessionId, ct) ?? throw new NotFoundException("Caja no encontrada.");
        var movements = await _cashMovements.ListBySessionAsync(shopId, sessionId, ct);
        var opened = await _users.GetByIdAsync(session.OpenedByUserId, ct);
        var closed = session.ClosedByUserId is null ? null : await _users.GetByIdAsync(session.ClosedByUserId.Value, ct);
        var summary = session.ClosingSummaryJson is null
            ? CashRegisterService.BuildSummary(session, movements, null, null)
            : JsonSerializer.Deserialize<List<Contracts.CashSummaryLine>>(session.ClosingSummaryJson) ?? new List<Contracts.CashSummaryLine>();

        var model = new CashReportModel(await DocShopAsync(shop, ct), session.Number, session.IsOpen ? "Abierta" : "Cerrada", Local(session.OpenedAtUtc, tz),
            opened?.DisplayName, session.ClosedAtUtc is null ? null : Local(session.ClosedAtUtc.Value, tz), closed?.DisplayName, session.OpeningCash, session.Currency,
            summary, movements.Select(m => new DocKeyValue($"{Local(m.CreatedAtUtc, tz):HH:mm} {m.Description} ({MethodLabel(m.Method)})", $"{(m.IsInflow ? "+" : "-")}{Format(m.Amount)} {m.Currency}")).ToList(),
            session.ExpectedCash, session.CountedCash, session.Difference, session.ClosingNotes);
        return new GeneratedDocument(_pdf.CashReport(model), $"caja-{session.Number:D5}.pdf");
    }

    // ===== Public (customer portal) =====

    public async Task<GeneratedDocument> PublicQuoteAsync(RepairOrder order, CancellationToken ct) => await QuoteAsync(order.ShopId, order.Id, null, ct);

    public async Task<GeneratedDocument> PublicWarrantyAsync(RepairOrder order, CancellationToken ct) => await WarrantyCertificateAsync(order.ShopId, order.Id, ct);

    // ---------------------------------------------------------------------------------------------

    public static string ArcaQrUrl(FiscalInvoice invoice, string? cuit)
    {
        var payload = new
        {
            ver = 1,
            fecha = invoice.IssueDateUtc.ToString("yyyy-MM-dd"),
            cuit = long.TryParse(cuit, out var c) ? c : 0,
            ptoVta = invoice.PointOfSale,
            tipoCmp = (int)invoice.VoucherType,
            nroCmp = invoice.Number ?? 0,
            importe = invoice.Total,
            moneda = "PES",
            ctz = 1,
            tipoDocRec = FiscalService.ArcaDocType(invoice.ReceiverDocumentType),
            nroDocRec = long.TryParse(invoice.ReceiverDocumentNumber, out var d) ? d : 0,
            tipoCodAut = "E",
            codAut = long.TryParse(invoice.Cae, out var cae) ? cae : 0
        };
        return "https://www.afip.gob.ar/fe/qr/?p=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
    }

    private async Task<(Shop Shop, RepairOrder Order, TimeZoneInfo Tz)> OrderContextAsync(Guid shopId, Guid orderId, CancellationToken ct)
    {
        var shop = await RequireShopAsync(shopId, ct);
        var order = await _orders.GetByIdAsync(shopId, orderId, ct) ?? throw new NotFoundException("Orden no encontrada.");
        return (shop, order, RenderOrderMessageService.ResolveTimeZone(shop.TimeZone));
    }

    private async Task<Shop> RequireShopAsync(Guid shopId, CancellationToken ct)
        => await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");

    private async Task<DocShop> DocShopAsync(Shop s, CancellationToken ct)
        => new(s.Name, s.LegalName, s.TaxId, TaxConditionLabel(s.TaxCondition), s.AddressLine, s.City, s.Phone, s.Email,
            await _files.ReadBytesAsync(s.Id, s.LogoFileId, ct));

    private static DocParty Party(Customer? c)
        => c is null ? new DocParty("—", null, null, null)
            : new DocParty(c.FullName, c.Phone, c.Email, c.DocumentNumber is null ? null : $"{c.DocumentType}: {c.DocumentNumber}");

    private static string DeviceName(Domain.Devices.Device? d) => d is null ? "—" : d.DisplayName;

    private static DateTime Local(DateTime utc, TimeZoneInfo tz) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

    private static string Format(decimal amount) => RenderOrderMessageService.Format(amount);

    public static string MethodLabel(PaymentMethod m) => m switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Transfer => "Transferencia",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.MercadoPago => "Mercado Pago",
        _ => "Otro"
    };

    private static string UnlockLabel(UnlockMethod m) => m switch
    {
        UnlockMethod.Pin => "PIN",
        UnlockMethod.Password => "Contraseña",
        UnlockMethod.Pattern => "Patrón",
        _ => "Código"
    };

    public static string TaxConditionLabel(CustomerTaxCondition c) => c switch
    {
        CustomerTaxCondition.ResponsableInscripto => "IVA Responsable Inscripto",
        CustomerTaxCondition.Monotributo => "Responsable Monotributo",
        CustomerTaxCondition.Exento => "IVA Exento",
        _ => "Consumidor Final"
    };

    private const string DefaultReceptionTerms =
        "1) El presupuesto se informa luego del diagnóstico y debe ser aprobado por el cliente antes de reparar. " +
        "2) El local no se responsabiliza por la información almacenada en el equipo: se recomienda realizar un backup. " +
        "3) Los equipos no retirados dentro de los 90 días de notificada su reparación podrán generar cargos de depósito. " +
        "4) Para retirar el equipo es indispensable presentar este comprobante o el DNI del titular.";

    private const string DefaultWarrantyTerms =
        "La garantía cubre fallas del repuesto colocado y de la mano de obra realizada. No cubre golpes, humedad, " +
        "roturas, manipulación por terceros ni problemas de software ajenos a la reparación.";
}
