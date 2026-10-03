using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RepairShop.Application.Abstractions;

namespace RepairShop.Infrastructure.Documents;

/// <summary>PDF documents rendered with QuestPDF (Community license) and QR codes with QRCoder.</summary>
public sealed class QuestPdfRenderer : IPdfRenderer
{
    private static readonly NumberFormatInfo Ar = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };
    private const string Accent = "#0f172a";
    private const string Muted = "#64748b";
    private const string Border = "#e2e8f0";

    static QuestPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ===== Intake receipt (A4) =====

    public byte[] IntakeReceipt(IntakeReceiptModel m) => Document.Create(doc => doc.Page(page =>
    {
        A4(page);
        page.Header().Element(c => Header(c, m.Shop, "Comprobante de ingreso", m.OrderCode, m.CreatedAtLocal));
        page.Content().PaddingVertical(10).Column(col =>
        {
            col.Spacing(10);
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => Box(c, "Cliente", x =>
                {
                    x.Item().Text(m.Customer.Name).SemiBold();
                    if (m.Customer.Phone is not null) x.Item().Text($"Tel: {m.Customer.Phone}");
                    if (m.Customer.Email is not null) x.Item().Text(m.Customer.Email);
                    if (m.Customer.Document is not null) x.Item().Text(m.Customer.Document);
                }));
                row.ConstantItem(10);
                row.RelativeItem().Element(c => Box(c, "Equipo", x =>
                {
                    x.Item().Text(m.Device).SemiBold();
                    if (m.Serial is not null) x.Item().Text($"N° de serie: {m.Serial}");
                    if (m.Imei is not null) x.Item().Text($"IMEI: {m.Imei}");
                    if (m.UnlockInfo is not null) x.Item().Text(m.UnlockInfo).FontColor(Muted);
                }));
            });

            col.Item().Element(c => Box(c, "Problema informado", x =>
            {
                x.Item().Text(m.Issue);
                if (!string.IsNullOrWhiteSpace(m.Notes)) x.Item().PaddingTop(4).Text($"Notas: {m.Notes}").FontColor(Muted);
            }));

            if (m.Checklist.Count > 0 || !string.IsNullOrWhiteSpace(m.CosmeticNotes))
            {
                col.Item().Element(c => Box(c, "Estado al ingresar", x =>
                {
                    x.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                        foreach (var kv in m.Checklist)
                        {
                            t.Cell().PaddingVertical(2).Text(kv.Label).FontColor(Muted);
                            t.Cell().PaddingVertical(2).Text(kv.Value).SemiBold();
                        }
                    });
                    if (!string.IsNullOrWhiteSpace(m.CosmeticNotes)) x.Item().PaddingTop(4).Text($"Estética: {m.CosmeticNotes}");
                }));
            }

            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => Box(c, "Datos de la orden", x =>
                {
                    if (m.PromisedAtLocal is not null) x.Item().Text($"Fecha estimada: {m.PromisedAtLocal:dd/MM/yyyy}");
                    if (m.TechnicianName is not null) x.Item().Text($"Técnico: {m.TechnicianName}");
                    foreach (var p in m.Payments) x.Item().Text($"{p.Label}: {p.Value}");
                    x.Item().PaddingTop(4).Text("Seguí el estado de tu reparación escaneando el código QR.").FontColor(Muted).FontSize(8);
                }));
                row.ConstantItem(10);
                row.ConstantItem(110).AlignCenter().Image(Qr(m.TrackingUrl));
            });

            if (!string.IsNullOrWhiteSpace(m.Terms))
                col.Item().Element(c => Box(c, "Términos y condiciones", x => x.Item().Text(m.Terms).FontSize(7.5f).FontColor(Muted)));

            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Element(c => SignatureBlock(c, m.Signature, m.SignedBy, "Firma del cliente"));
                row.ConstantItem(30);
                row.RelativeItem().Element(c => SignatureBlock(c, null, m.Shop.Name, "Recibido por"));
            });
        });
        page.Footer().Element(Footer);
    })).GeneratePdf();

    // ===== Label (62 x 40 mm sticker) =====

    public byte[] Label(LabelModel m) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(62, 40, Unit.Millimetre);
        page.Margin(2, Unit.Millimetre);
        page.DefaultTextStyle(x => x.FontSize(7));
        page.Content().Row(row =>
        {
            row.ConstantItem(18, Unit.Millimetre).AlignMiddle().Image(Qr(m.TrackingUrl));
            row.ConstantItem(2, Unit.Millimetre);
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(m.OrderCode).FontSize(13).Bold();
                col.Item().Text(m.CustomerName).SemiBold().ClampLines(1);
                col.Item().Text(m.Device).ClampLines(1);
                col.Item().Text(m.Issue).FontColor(Muted).ClampLines(2);
                col.Item().Text($"{m.ShopName} · {m.CreatedAtLocal:dd/MM/yy}").FontSize(6).FontColor(Muted);
            });
        });
    })).GeneratePdf();

    // ===== Quote (A4) =====

    public byte[] Quote(QuoteDocumentModel m) => Document.Create(doc => doc.Page(page =>
    {
        A4(page);
        page.Header().Element(c => Header(c, m.Shop, $"Presupuesto (v{m.Version})", m.OrderCode, m.CreatedAtLocal));
        page.Content().PaddingVertical(10).Column(col =>
        {
            col.Spacing(10);
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => Box(c, "Cliente", x => { x.Item().Text(m.Customer.Name).SemiBold(); if (m.Customer.Phone is not null) x.Item().Text(m.Customer.Phone); }));
                row.ConstantItem(10);
                row.RelativeItem().Element(c => Box(c, "Equipo", x => { x.Item().Text(m.Device).SemiBold(); x.Item().Text(m.Issue).FontColor(Muted); }));
            });

            col.Item().Element(c => Lines(c, m.Lines, m.Currency, showDiscount: false));
            col.Item().AlignRight().Width(220).Element(c => Totals(c, m.Subtotal, m.Discount, m.Total, m.Currency));

            col.Item().Row(row =>
            {
                row.RelativeItem().Column(x =>
                {
                    x.Item().Text($"Estado: {m.StatusLabel}").SemiBold();
                    if (m.ValidUntilLocal is not null) x.Item().Text($"Válido hasta: {m.ValidUntilLocal:dd/MM/yyyy}");
                    if (m.WarrantyDays is not null) x.Item().Text($"Garantía: {m.WarrantyDays} días");
                    if (!string.IsNullOrWhiteSpace(m.Notes)) x.Item().PaddingTop(4).Text(m.Notes).FontColor(Muted);
                    x.Item().PaddingTop(6).Text("Podés aprobarlo o rechazarlo desde el link de seguimiento (código QR).").FontSize(8).FontColor(Muted);
                });
                row.ConstantItem(100).Image(Qr(m.TrackingUrl));
            });
        });
        page.Footer().Element(Footer);
    })).GeneratePdf();

    // ===== Payment receipt (A4 half) =====

    public byte[] PaymentReceipt(PaymentReceiptModel m) => Document.Create(doc => doc.Page(page =>
    {
        A4(page);
        page.Header().Element(c => Header(c, m.Shop, m.IsRefund ? "Comprobante de devolución" : "Recibo de pago", m.ReceiptCode, m.DateLocal));
        page.Content().PaddingVertical(10).Column(col =>
        {
            col.Spacing(10);
            col.Item().Element(c => Box(c, "Recibimos de", x =>
            {
                x.Item().Text(m.Customer.Name).SemiBold();
                if (m.Customer.Document is not null) x.Item().Text(m.Customer.Document);
            }));
            col.Item().Element(c => Box(c, "Concepto", x =>
            {
                x.Item().Text(m.Concept);
                if (m.Device is not null) x.Item().Text(m.Device).FontColor(Muted);
                x.Item().Text($"Medio de pago: {m.Method}{(m.Reference is null ? "" : $" · Ref: {m.Reference}")}");
            }));
            col.Item().AlignRight().Text($"{(m.IsRefund ? "-" : "")}{Money(m.Amount)} {m.Currency}").FontSize(20).Bold();
            col.Item().AlignRight().Width(260).Column(x =>
            {
                Pair(x, "Total de la orden", $"{Money(m.OrderTotal)} {m.Currency}");
                Pair(x, "Pagado a la fecha", $"{Money(m.Paid)} {m.Currency}");
                Pair(x, "Saldo pendiente", $"{Money(m.Balance)} {m.Currency}", bold: true);
            });
            col.Item().PaddingTop(20).Text("Documento no válido como factura.").FontSize(8).FontColor(Muted);
        });
        page.Footer().Element(Footer);
    })).GeneratePdf();

    // ===== Sale ticket (80 mm thermal) =====

    public byte[] SaleTicket(SaleTicketModel m) => Document.Create(doc => doc.Page(page =>
    {
        page.ContinuousSize(80, Unit.Millimetre);
        page.Margin(4, Unit.Millimetre);
        page.DefaultTextStyle(x => x.FontSize(8));
        page.Content().Column(col =>
        {
            col.Spacing(3);
            if (m.Shop.Logo is not null) col.Item().AlignCenter().Height(30).Image(m.Shop.Logo).FitHeight();
            col.Item().AlignCenter().Text(m.Shop.Name).Bold().FontSize(11);
            if (m.Shop.LegalName is not null) col.Item().AlignCenter().Text(m.Shop.LegalName);
            if (m.Shop.TaxId is not null) col.Item().AlignCenter().Text($"CUIT {m.Shop.TaxId}");
            if (m.Shop.Address is not null) col.Item().AlignCenter().Text($"{m.Shop.Address} {m.Shop.City}");
            col.Item().LineHorizontal(0.5f);
            col.Item().Row(r => { r.RelativeItem().Text($"Ticket {m.Code}").SemiBold(); r.AutoItem().Text($"{m.DateLocal:dd/MM/yyyy HH:mm}"); });
            if (!string.IsNullOrWhiteSpace(m.Status)) col.Item().AlignCenter().Text(m.Status).Bold().FontColor(Colors.Red.Medium);
            if (m.CustomerName is not null) col.Item().Text($"Cliente: {m.CustomerName}");
            col.Item().LineHorizontal(0.5f);
            foreach (var l in m.Lines)
            {
                col.Item().Text(l.Description).SemiBold();
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text($"{l.Quantity:0.##} x {Money(l.UnitPrice)}{(l.Discount > 0 ? $" (-{Money(l.Discount)})" : "")}");
                    r.AutoItem().Text(Money(l.Total));
                });
            }
            col.Item().LineHorizontal(0.5f);
            col.Item().Row(r => { r.RelativeItem().Text("Subtotal"); r.AutoItem().Text(Money(m.Subtotal)); });
            if (m.Discount > 0) col.Item().Row(r => { r.RelativeItem().Text("Descuento"); r.AutoItem().Text($"-{Money(m.Discount)}"); });
            col.Item().Row(r => { r.RelativeItem().Text("TOTAL").Bold().FontSize(11); r.AutoItem().Text($"{Money(m.Total)} {m.Currency}").Bold().FontSize(11); });
            foreach (var p in m.Payments) col.Item().Row(r => { r.RelativeItem().Text(p.Label); r.AutoItem().Text(p.Value); });
            if (m.Change > 0) col.Item().Row(r => { r.RelativeItem().Text("Vuelto"); r.AutoItem().Text(Money(m.Change)); });
            col.Item().LineHorizontal(0.5f);
            if (m.CashierName is not null) col.Item().Text($"Atendió: {m.CashierName}").FontColor(Muted);
            foreach (var f in m.Footer) col.Item().AlignCenter().Text(f).FontSize(7).FontColor(Muted);
            col.Item().AlignCenter().Text("Documento no válido como factura").FontSize(7).FontColor(Muted);
        });
    })).GeneratePdf();

    // ===== Warranty certificate (A4) =====

    public byte[] WarrantyCertificate(WarrantyCertificateModel m) => Document.Create(doc => doc.Page(page =>
    {
        A4(page);
        page.Header().Element(c => Header(c, m.Shop, "Certificado de garantía", m.OrderCode, m.DeliveredLocal));
        page.Content().PaddingVertical(10).Column(col =>
        {
            col.Spacing(10);
            col.Item().Background("#f1f5f9").Padding(12).Row(row =>
            {
                row.RelativeItem().Column(x =>
                {
                    x.Item().Text($"{m.Days} días de garantía").FontSize(18).Bold();
                    x.Item().Text(m.ExpiresLocal is null ? "Sin vencimiento" : $"Vigente hasta el {m.ExpiresLocal:dd/MM/yyyy}");
                    x.Item().Text($"Entregado el {m.DeliveredLocal:dd/MM/yyyy}").FontColor(Muted);
                });
                row.ConstantItem(90).Image(Qr(m.TrackingUrl));
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => Box(c, "Cliente", x => { x.Item().Text(m.Customer.Name).SemiBold(); if (m.Customer.Phone is not null) x.Item().Text(m.Customer.Phone); }));
                row.ConstantItem(10);
                row.RelativeItem().Element(c => Box(c, "Equipo", x => { x.Item().Text(m.Device).SemiBold(); if (m.Serial is not null) x.Item().Text($"Serie/IMEI: {m.Serial}"); }));
            });
            col.Item().Element(c => Box(c, "Trabajo realizado", x =>
            {
                x.Item().Text(m.Issue).FontColor(Muted);
                foreach (var w in m.Work) x.Item().Text($"• {w}");
            }));
            if (!string.IsNullOrWhiteSpace(m.Terms)) col.Item().Element(c => Box(c, "Condiciones", x => x.Item().Text(m.Terms).FontSize(8).FontColor(Muted)));
            col.Item().PaddingTop(10).Width(250).Element(c => SignatureBlock(c, m.Signature, m.SignedBy, "Recibí conforme"));
        });
        page.Footer().Element(Footer);
    })).GeneratePdf();

    // ===== Electronic invoice (A4) =====

    public byte[] Invoice(InvoiceDocumentModel m) => Document.Create(doc => doc.Page(page =>
    {
        A4(page);
        page.Header().Column(h =>
        {
            h.Item().Row(row =>
            {
                row.RelativeItem().Column(x =>
                {
                    if (m.Shop.Logo is not null) x.Item().Height(40).Image(m.Shop.Logo).FitHeight();
                    x.Item().Text(m.Shop.LegalName ?? m.Shop.Name).Bold().FontSize(13);
                    x.Item().Text($"{m.Shop.Address} {m.Shop.City}");
                    x.Item().Text(m.Shop.TaxCondition ?? "");
                });
                row.ConstantItem(60).Border(1).BorderColor(Accent).AlignCenter().AlignMiddle().Column(x =>
                {
                    x.Item().AlignCenter().Text(m.Letter).FontSize(28).Bold();
                    x.Item().AlignCenter().Text($"COD. {m.VoucherCode:D2}").FontSize(7);
                });
                row.RelativeItem().AlignRight().Column(x =>
                {
                    x.Item().AlignRight().Text(m.VoucherName).Bold().FontSize(13);
                    x.Item().AlignRight().Text($"N° {m.PointOfSale:D5}-{(m.Number is null ? "--------" : m.Number.Value.ToString("D8"))}");
                    x.Item().AlignRight().Text($"Fecha: {m.DateLocal:dd/MM/yyyy}");
                    if (m.Shop.TaxId is not null) x.Item().AlignRight().Text($"CUIT: {m.Shop.TaxId}");
                });
            });
            h.Item().PaddingTop(6).LineHorizontal(1).LineColor(Border);
        });
        page.Content().PaddingVertical(10).Column(col =>
        {
            col.Spacing(10);
            col.Item().Element(c => Box(c, "Receptor", x =>
            {
                x.Item().Text(m.Receiver.Name).SemiBold();
                x.Item().Text(m.ReceiverTaxCondition);
                if (m.Receiver.Document is not null) x.Item().Text(m.Receiver.Document);
            }));
            col.Item().Element(c => Lines(c, m.Lines, "ARS", showDiscount: true));
            col.Item().AlignRight().Width(240).Column(x =>
            {
                if (m.Vat > 0)
                {
                    Pair(x, "Importe neto gravado", Money(m.Net));
                    Pair(x, "IVA 21%", Money(m.Vat));
                }
                Pair(x, "Importe total", $"$ {Money(m.Total)}", bold: true);
            });
            col.Item().Row(row =>
            {
                if (m.QrUrl is not null) row.ConstantItem(90).Image(Qr(m.QrUrl));
                row.RelativeItem().PaddingLeft(10).AlignMiddle().Column(x =>
                {
                    if (m.Cae is not null)
                    {
                        x.Item().Text($"CAE N°: {m.Cae}").Bold();
                        if (m.CaeDue is not null) x.Item().Text($"Vencimiento CAE: {m.CaeDue:dd/MM/yyyy}");
                        x.Item().Text("Comprobante autorizado por ARCA").FontColor(Muted);
                    }
                    else
                    {
                        x.Item().Text($"Comprobante NO autorizado ({m.Status})").Bold().FontColor(Colors.Red.Medium);
                    }
                });
            });
        });
        page.Footer().Element(Footer);
    })).GeneratePdf();

    // ===== Cash register report (A4) =====

    public byte[] CashReport(CashReportModel m) => Document.Create(doc => doc.Page(page =>
    {
        A4(page);
        page.Header().Element(c => Header(c, m.Shop, $"Arqueo de caja N° {m.Number}", m.Status, m.ClosedAtLocal ?? m.OpenedAtLocal));
        page.Content().PaddingVertical(10).Column(col =>
        {
            col.Spacing(10);
            col.Item().Element(c => Box(c, "Turno", x =>
            {
                x.Item().Text($"Apertura: {m.OpenedAtLocal:dd/MM/yyyy HH:mm} por {m.OpenedBy}");
                if (m.ClosedAtLocal is not null) x.Item().Text($"Cierre: {m.ClosedAtLocal:dd/MM/yyyy HH:mm} por {m.ClosedBy}");
                x.Item().Text($"Fondo inicial: {Money(m.OpeningCash)} {m.Currency}");
            }));
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                t.Cell().Element(HeaderCell).Text("Medio / moneda");
                t.Cell().Element(HeaderCell).AlignRight().Text("Ingresos");
                t.Cell().Element(HeaderCell).AlignRight().Text("Egresos");
                t.Cell().Element(HeaderCell).AlignRight().Text("Esperado");
                t.Cell().Element(HeaderCell).AlignRight().Text("Declarado");
                t.Cell().Element(HeaderCell).AlignRight().Text("Diferencia");
                foreach (var s in m.Summary)
                {
                    t.Cell().Element(BodyCell).Text($"{s.Method} ({s.Currency})");
                    t.Cell().Element(BodyCell).AlignRight().Text(Money(s.Inflows));
                    t.Cell().Element(BodyCell).AlignRight().Text(Money(s.Outflows));
                    t.Cell().Element(BodyCell).AlignRight().Text(Money(s.Expected));
                    t.Cell().Element(BodyCell).AlignRight().Text(s.Declared is null ? "—" : Money(s.Declared.Value));
                    t.Cell().Element(BodyCell).AlignRight().Text(s.Difference is null ? "—" : Money(s.Difference.Value));
                }
            });
            if (m.Expected is not null)
                col.Item().AlignRight().Text($"Efectivo esperado {Money(m.Expected.Value)} · contado {Money(m.Counted ?? 0)} · diferencia {Money(m.Difference ?? 0)}").SemiBold();
            if (!string.IsNullOrWhiteSpace(m.Notes)) col.Item().Text($"Notas: {m.Notes}").FontColor(Muted);
            col.Item().Element(c => Box(c, "Movimientos", x =>
            {
                foreach (var mv in m.Movements)
                    x.Item().Row(r => { r.RelativeItem().Text(mv.Label).FontSize(8); r.AutoItem().Text(mv.Value).FontSize(8); });
            }));
        });
        page.Footer().Element(Footer);
    })).GeneratePdf();

    // ---------------------------------------------------------------------------------------------

    private static void A4(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(28);
        page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor("#0f172a"));
    }

    private static void Header(IContainer c, DocShop shop, string title, string code, DateTime date)
        => c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Row(left =>
                {
                    if (shop.Logo is not null) left.ConstantItem(55).Height(45).Image(shop.Logo).FitArea();
                    left.RelativeItem().PaddingLeft(shop.Logo is null ? 0 : 8).Column(x =>
                    {
                        x.Item().Text(shop.Name).Bold().FontSize(14);
                        if (shop.LegalName is not null && shop.LegalName != shop.Name) x.Item().Text(shop.LegalName).FontColor(Muted);
                        var line = string.Join(" · ", new[] { shop.Address, shop.City, shop.Phone }.Where(v => !string.IsNullOrWhiteSpace(v)));
                        if (line.Length > 0) x.Item().Text(line).FontColor(Muted).FontSize(8);
                        if (shop.TaxId is not null) x.Item().Text($"CUIT {shop.TaxId} · {shop.TaxCondition}").FontColor(Muted).FontSize(8);
                    });
                });
                row.ConstantItem(190).AlignRight().Column(x =>
                {
                    x.Item().AlignRight().Text(title).FontSize(12).SemiBold();
                    x.Item().AlignRight().Text(code).FontSize(16).Bold();
                    x.Item().AlignRight().Text(date.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)).FontColor(Muted);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Border);
        });

    private static void Box(IContainer c, string title, Action<ColumnDescriptor> content)
        => c.Border(1).BorderColor(Border).Padding(8).Column(col =>
        {
            col.Spacing(2);
            col.Item().Text(title.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(Muted);
            content(col);
        });

    private static void Lines(IContainer c, IReadOnlyList<DocLine> lines, string currency, bool showDiscount)
        => c.Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(5);
                cd.RelativeColumn(1);
                cd.RelativeColumn(2);
                if (showDiscount) cd.RelativeColumn(2);
                cd.RelativeColumn(2);
            });
            t.Cell().Element(HeaderCell).Text("Descripción");
            t.Cell().Element(HeaderCell).AlignRight().Text("Cant.");
            t.Cell().Element(HeaderCell).AlignRight().Text("Precio");
            if (showDiscount) t.Cell().Element(HeaderCell).AlignRight().Text("Desc.");
            t.Cell().Element(HeaderCell).AlignRight().Text($"Total {currency}");
            foreach (var l in lines)
            {
                t.Cell().Element(BodyCell).Text(l.Description);
                t.Cell().Element(BodyCell).AlignRight().Text(l.Quantity.ToString("0.##", CultureInfo.InvariantCulture));
                t.Cell().Element(BodyCell).AlignRight().Text(Money(l.UnitPrice));
                if (showDiscount) t.Cell().Element(BodyCell).AlignRight().Text(l.Discount == 0 ? "—" : Money(l.Discount));
                t.Cell().Element(BodyCell).AlignRight().Text(Money(l.Total));
            }
        });

    private static void Totals(IContainer c, decimal subtotal, decimal discount, decimal total, string currency)
        => c.Column(x =>
        {
            Pair(x, "Subtotal", Money(subtotal));
            if (discount > 0) Pair(x, "Descuento", $"-{Money(discount)}");
            Pair(x, "Total", $"{Money(total)} {currency}", bold: true);
        });

    private static void Pair(ColumnDescriptor x, string label, string value, bool bold = false)
        => x.Item().Row(r =>
        {
            var l = r.RelativeItem().Text(label);
            var v = r.AutoItem().Text(value);
            if (bold) { l.Bold(); v.Bold(); }
        });

    private static void SignatureBlock(IContainer c, byte[]? signature, string? name, string caption)
        => c.Column(col =>
        {
            col.Item().Height(55).AlignBottom().AlignCenter().Element(e =>
            {
                if (signature is not null) e.Image(signature).FitArea();
            });
            col.Item().LineHorizontal(0.75f);
            col.Item().AlignCenter().Text(caption).FontSize(8).FontColor(Muted);
            if (!string.IsNullOrWhiteSpace(name)) col.Item().AlignCenter().Text(name).FontSize(8);
        });

    private static IContainer HeaderCell(IContainer c) => c.BorderBottom(1).BorderColor(Accent).PaddingVertical(4).PaddingHorizontal(2).DefaultTextStyle(x => x.SemiBold().FontSize(8.5f));

    private static IContainer BodyCell(IContainer c) => c.BorderBottom(0.5f).BorderColor(Border).PaddingVertical(3).PaddingHorizontal(2);

    private static void Footer(IContainer c)
        => c.AlignCenter().Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(7).FontColor(Muted));
            t.Span("Generado con RepairShop · Página ");
            t.CurrentPageNumber();
            t.Span(" de ");
            t.TotalPages();
        });

    private static string Money(decimal amount) => amount.ToString("#,0.00", Ar);

    public static byte[] Qr(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }
}
