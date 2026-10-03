using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RepairShop.Domain.Common;
using RepairShop.Domain.RepairOrders;
using RepairShop.Domain.Saas;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public sealed class FiscalOptions
{
    public const string Section = "Saas:Fiscal";
    // Simulated (non-fiscal receipts everywhere) | Arca (shops with a certificate issue real vouchers)
    public string Provider { get; set; } = "Simulated";
}

public sealed record FiscalLine(string Description, decimal Quantity, decimal UnitPrice);
public sealed record FiscalRequest(string EmitterTaxId, string EmitterCondition, int PointOfSale, int VoucherType, string Concept, int DocType, string DocNumber,
    string ReceiverCondition, decimal Total, decimal VatRate, string Currency, IReadOnlyList<FiscalLine> Lines, FiscalInvoice? Cancels);
public sealed record FiscalResult(string Status, long Number, string Cae, DateTime? CaeDueDate, decimal Net, decimal Vat, decimal ExchangeRate, string Message, string Environment);

public interface IFiscalProvider
{
    Task<FiscalResult> AuthorizeAsync(FiscalSettings settings, FiscalRequest request, CancellationToken ct);
    Task<string> TestAsync(FiscalSettings settings, string taxId, CancellationToken ct);
}

public static class FiscalRules
{
    public static bool IsCreditNote(int type) => type is 3 or 8 or 13;

    public static int VoucherType(string emitterCondition, string receiverCondition, bool creditNote = false)
    {
        var letter = emitterCondition == "ResponsableInscripto"
            ? receiverCondition is "ResponsableInscripto" or "Monotributo" ? 'A' : 'B'
            : 'C';
        return (letter, creditNote) switch { ('A', false) => 1, ('A', true) => 3, ('B', false) => 6, ('B', true) => 8, (_, false) => 11, (_, true) => 13 };
    }

    public static string Name(int type) => type switch
    {
        1 => "Factura A", 3 => "Nota de crédito A", 6 => "Factura B", 8 => "Nota de crédito B", 11 => "Factura C", 13 => "Nota de crédito C", _ => $"Comprobante {type}",
    };

    public static (decimal Net, decimal Vat) Split(int voucherType, decimal total, decimal rate)
    {
        if (voucherType is 11 or 13 || rate <= 0) return (total, 0m);
        var net = decimal.Round(total / (1 + rate / 100m), 2, MidpointRounding.AwayFromZero);
        return (net, total - net);
    }

    // RG 5616: receiver VAT condition id.
    public static int ReceiverConditionId(string condition) => condition switch
    {
        "ResponsableInscripto" => 1, "Exento" => 4, "Monotributo" => 6, _ => 5,
    };

    public static int VatId(decimal rate) => rate switch { 0m => 3, 10.5m => 4, 21m => 5, 27m => 6, 5m => 8, 2.5m => 9, _ => throw new DomainException("Alícuota de IVA no soportada.") };

    public static bool ValidCuit(string cuit)
    {
        if (cuit.Length != 11 || !cuit.All(char.IsDigit)) return false;
        int[] weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var sum = weights.Select((w, i) => w * (cuit[i] - '0')).Sum();
        var check = 11 - sum % 11;
        check = check == 11 ? 0 : check == 10 ? 9 : check;
        return check == cuit[10] - '0';
    }
}

// Receipts for shops without ARCA: numbered like a voucher but clearly marked as non-fiscal.
public sealed class SimulatedFiscalProvider(RepairShopDbContext db) : IFiscalProvider
{
    public async Task<FiscalResult> AuthorizeAsync(FiscalSettings settings, FiscalRequest r, CancellationToken ct)
    {
        var last = await db.FiscalInvoices.Where(x => x.ShopId == settings.ShopId && x.Status == "Simulated" && x.VoucherType == r.VoucherType && x.PointOfSale == r.PointOfSale)
            .MaxAsync(x => (long?)x.Number, ct) ?? 0;
        var (net, vat) = FiscalRules.Split(r.VoucherType, r.Total, r.VatRate);
        return new FiscalResult("Simulated", last + 1, "", null, net, vat, 1m, "Comprobante interno sin validez fiscal. Configurá ARCA para emitir facturas electrónicas.", "Interno");
    }

    public Task<string> TestAsync(FiscalSettings settings, string taxId, CancellationToken ct) =>
        Task.FromResult("La facturación está en modo interno (sin ARCA) en este servidor.");
}

// ARCA (ex AFIP) web services: WSAA for the access ticket and WSFEv1 for electronic vouchers.
public sealed class ArcaFiscalProvider(HttpClient http, IMemoryCache cache, IDataProtectionProvider protection, ILogger<ArcaFiscalProvider> log) : IFiscalProvider
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Fe = "http://ar.gov.afip.dif.FEV1/";
    private static readonly XNamespace Wsaa = "http://wsaa.view.sua.dvadac.desein.afip.gov";

    private static (string Wsaa, string Wsfe) Urls(string env) => env == "Produccion"
        ? ("https://wsaa.afip.gov.ar/ws/services/LoginCms", "https://servicios1.afip.gov.ar/wsfev1/service.asmx")
        : ("https://wsaahomo.afip.gov.ar/ws/services/LoginCms", "https://wswhomo.afip.gov.ar/wsfev1/service.asmx");

    private X509Certificate2 Certificate(FiscalSettings s)
    {
        if (string.IsNullOrEmpty(s.ProtectedCertificate) || string.IsNullOrEmpty(s.ProtectedPrivateKey)) throw new DomainException("Cargá el certificado y la clave privada de ARCA.");
        var p = protection.CreateProtector(FiscalController.Purpose);
        using var pem = X509Certificate2.CreateFromPem(p.Unprotect(s.ProtectedCertificate), p.Unprotect(s.ProtectedPrivateKey));
        // Re-import so the private key is usable by SignedCms on every platform.
        return new X509Certificate2(pem.Export(X509ContentType.Pkcs12));
    }

    private async Task<(string Token, string Sign)> Ticket(FiscalSettings s, CancellationToken ct)
    {
        var key = $"arca:ta:{s.ShopId}:{s.Environment}:{s.Version}";
        if (cache.TryGetValue(key, out (string, string) ta)) return ta;
        var now = DateTime.UtcNow;
        var tra = new XElement("loginTicketRequest", new XAttribute("version", "1.0"),
            new XElement("header",
                new XElement("uniqueId", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                new XElement("generationTime", now.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
                new XElement("expirationTime", now.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))),
            new XElement("service", "wsfe"));
        using var cert = Certificate(s);
        var cms = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes(tra.ToString(SaveOptions.DisableFormatting))));
        cms.ComputeSignature(new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, cert) { IncludeOption = X509IncludeOption.EndCertOnly });
        var envelope = new XElement(Soap + "Envelope", new XAttribute(XNamespace.Xmlns + "soapenv", Soap), new XAttribute(XNamespace.Xmlns + "wsaa", Wsaa),
            new XElement(Soap + "Header"), new XElement(Soap + "Body", new XElement(Wsaa + "loginCms", new XElement(Wsaa + "in0", Convert.ToBase64String(cms.Encode())))));
        var body = await Post(Urls(s.Environment).Wsaa, "", envelope, ct);
        var ret = body.Descendants().FirstOrDefault(e => e.Name.LocalName == "loginCmsReturn")?.Value
            ?? throw new DomainException("ARCA no devolvió el ticket de acceso: " + Fault(body));
        var response = XDocument.Parse(ret);
        var token = response.Descendants("token").Single().Value;
        var sign = response.Descendants("sign").Single().Value;
        var expires = DateTime.Parse(response.Descendants("expirationTime").Single().Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        cache.Set(key, (token, sign), expires.AddMinutes(-5));
        return (token, sign);
    }

    private async Task<XDocument> Post(string url, string action, XElement envelope, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml") };
        req.Headers.Add("SOAPAction", $"\"{action}\"");
        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        try { return XDocument.Parse(text); }
        catch (System.Xml.XmlException) { throw new DomainException($"ARCA respondió {(int)res.StatusCode} con un contenido inesperado."); }
    }

    private static string Fault(XDocument doc) =>
        doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value ?? "respuesta vacía";

    private XElement Auth(string token, string sign, string cuit) =>
        new(Fe + "Auth", new XElement(Fe + "Token", token), new XElement(Fe + "Sign", sign), new XElement(Fe + "Cuit", cuit));

    private async Task<XDocument> Call(FiscalSettings s, string method, string cuit, params object[] content)
    {
        var (token, sign) = await Ticket(s, CancellationToken.None);
        var envelope = new XElement(Soap + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", Soap), new XAttribute(XNamespace.Xmlns + "ar", Fe),
            new XElement(Soap + "Body", new XElement(Fe + method, [Auth(token, sign, cuit), .. content])));
        return await Post(Urls(s.Environment).Wsfe, $"http://ar.gov.afip.dif.FEV1/{method}", envelope, CancellationToken.None);
    }

    private static string Errors(XDocument doc) => string.Join(" · ",
        doc.Descendants().Where(e => e.Name.LocalName is "Err" or "Obs").Select(e =>
            $"{e.Elements().FirstOrDefault(x => x.Name.LocalName == "Code")?.Value}: {e.Elements().FirstOrDefault(x => x.Name.LocalName == "Msg")?.Value}"));

    public async Task<string> TestAsync(FiscalSettings s, string cuit, CancellationToken ct)
    {
        var result = await Call(s, "FECompUltimoAutorizado", cuit, new XElement(Fe + "PtoVta", s.PointOfSale), new XElement(Fe + "CbteTipo", 11));
        var number = result.Descendants().FirstOrDefault(e => e.Name.LocalName == "CbteNro")?.Value;
        var errors = Errors(result);
        if (number is null) throw new DomainException("ARCA rechazó la consulta: " + (errors.Length > 0 ? errors : Fault(result)));
        return $"Conexión correcta con ARCA ({s.Environment}). Punto de venta {s.PointOfSale}: último comprobante C informado {number}.{(errors.Length > 0 ? " " + errors : "")}";
    }

    public async Task<FiscalResult> AuthorizeAsync(FiscalSettings s, FiscalRequest r, CancellationToken ct)
    {
        var last = await Call(s, "FECompUltimoAutorizado", r.EmitterTaxId, new XElement(Fe + "PtoVta", r.PointOfSale), new XElement(Fe + "CbteTipo", r.VoucherType));
        var lastNumber = long.Parse(last.Descendants().FirstOrDefault(e => e.Name.LocalName == "CbteNro")?.Value
            ?? throw new DomainException("ARCA no informó la numeración: " + Errors(last)), CultureInfo.InvariantCulture);
        var number = lastNumber + 1;
        var rate = 1m;
        var monId = r.Currency == "USD" ? "DOL" : "PES";
        if (monId == "DOL")
        {
            var quote = await Call(s, "FEParamGetCotizacion", r.EmitterTaxId, new XElement(Fe + "MonId", "DOL"));
            rate = decimal.Parse(quote.Descendants().FirstOrDefault(e => e.Name.LocalName == "MonCotiz")?.Value ?? throw new DomainException("ARCA no informó la cotización del dólar."), CultureInfo.InvariantCulture);
        }
        var (net, vat) = FiscalRules.Split(r.VoucherType, r.Total, r.VatRate);
        var today = SaasScheduler.Local(DateTime.UtcNow).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var concept = r.Concept == "Products" ? 1 : 2;
        string F(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        var detail = new XElement(Fe + "FECAEDetRequest",
            new XElement(Fe + "Concepto", concept),
            new XElement(Fe + "DocTipo", r.DocType),
            new XElement(Fe + "DocNro", r.DocType == 99 ? "0" : r.DocNumber),
            new XElement(Fe + "CbteDesde", number),
            new XElement(Fe + "CbteHasta", number),
            new XElement(Fe + "CbteFch", today),
            new XElement(Fe + "ImpTotal", F(r.Total)),
            new XElement(Fe + "ImpTotConc", "0.00"),
            new XElement(Fe + "ImpNeto", F(net)),
            new XElement(Fe + "ImpOpEx", "0.00"),
            new XElement(Fe + "ImpTrib", "0.00"),
            new XElement(Fe + "ImpIVA", F(vat)),
            concept == 1 ? null : new XElement(Fe + "FchServDesde", today),
            concept == 1 ? null : new XElement(Fe + "FchServHasta", today),
            concept == 1 ? null : new XElement(Fe + "FchVtoPago", today),
            new XElement(Fe + "MonId", monId),
            new XElement(Fe + "MonCotiz", rate.ToString("0.######", CultureInfo.InvariantCulture)),
            monId == "DOL" ? new XElement(Fe + "CanMisMonExt", "N") : null,
            new XElement(Fe + "CondicionIVAReceptorId", FiscalRules.ReceiverConditionId(r.ReceiverCondition)),
            r.Cancels is { } original ? new XElement(Fe + "CbtesAsoc", new XElement(Fe + "CbteAsoc",
                new XElement(Fe + "Tipo", original.VoucherType), new XElement(Fe + "PtoVta", original.PointOfSale), new XElement(Fe + "Nro", original.Number),
                new XElement(Fe + "Cuit", r.EmitterTaxId), new XElement(Fe + "CbteFch", SaasScheduler.Local(original.CreatedAtUtc).ToString("yyyyMMdd", CultureInfo.InvariantCulture)))) : null,
            vat == 0 ? null : new XElement(Fe + "Iva", new XElement(Fe + "AlicIva", new XElement(Fe + "Id", FiscalRules.VatId(r.VatRate)), new XElement(Fe + "BaseImp", F(net)), new XElement(Fe + "Importe", F(vat)))));
        var request = new XElement(Fe + "FeCAEReq",
            new XElement(Fe + "FeCabReq", new XElement(Fe + "CantReg", 1), new XElement(Fe + "PtoVta", r.PointOfSale), new XElement(Fe + "CbteTipo", r.VoucherType)),
            new XElement(Fe + "FeDetReq", detail));
        var response = await Call(s, "FECAESolicitar", r.EmitterTaxId, request);
        var result = response.Descendants().FirstOrDefault(e => e.Name.LocalName == "FECAEDetResponse");
        var outcome = result?.Elements().FirstOrDefault(e => e.Name.LocalName == "Resultado")?.Value;
        var messages = Errors(response);
        log.LogInformation("ARCA FECAESolicitar shop {Shop} type {Type} #{Number}: {Outcome} {Messages}", s.ShopId, r.VoucherType, number, outcome, messages);
        if (outcome != "A") return new FiscalResult("Rejected", number, "", null, net, vat, rate, messages.Length > 0 ? messages : Fault(response), s.Environment);
        var cae = result!.Elements().First(e => e.Name.LocalName == "CAE").Value;
        var due = DateTime.ParseExact(result.Elements().First(e => e.Name.LocalName == "CAEFchVto").Value, "yyyyMMdd", CultureInfo.InvariantCulture);
        return new FiscalResult("Authorized", number, cae, due, net, vat, rate, messages, s.Environment);
    }
}

public sealed record FiscalSettingsRequest(int Version, bool Enabled, string Environment, int PointOfSale, decimal DefaultVatRate, string? CertificatePem, string? PrivateKeyPem);
public sealed record IssueInvoiceRequest(string SourceType, Guid SourceId, string? CustomerName, int? DocType, string? DocNumber, string? TaxCondition);
public sealed record CreditNoteRequest(string Reason);

[ApiController, Microsoft.AspNetCore.Authorization.Authorize, Route("api/saas")]
public sealed class FiscalController(RepairShopDbContext db, IDataProtectionProvider protection, IOptions<FiscalOptions> options,
    SimulatedFiscalProvider simulated, ArcaFiscalProvider arca) : SaasController(db)
{
    public const string Purpose = "RepairShop.Fiscal.v1";

    private async Task<FiscalSettings> Settings()
    {
        var s = await Own<FiscalSettings>().SingleOrDefaultAsync();
        if (s is not null) return s;
        s = new FiscalSettings { ShopId = Shop };
        Db.FiscalSettings.Add(s);
        return s;
    }

    private IFiscalProvider Provider(FiscalSettings s) => options.Value.Provider == "Arca" && s.Enabled ? arca : simulated;

    [HttpGet("fiscal")]
    public async Task<IActionResult> GetSettings()
    {
        var s = await Settings();
        return Ok(new { data = new { s.Version, s.Enabled, s.Environment, s.PointOfSale, s.DefaultVatRate, s.CertificateExpiresAtUtc, HasCertificate = s.ProtectedCertificate.Length > 0, ServerProvider = options.Value.Provider } });
    }

    [HttpPut("fiscal")]
    public async Task<IActionResult> SaveSettings(FiscalSettingsRequest b)
    {
        RequireAdmin();
        var s = await Settings();
        if (Db.Entry(s).State != EntityState.Added) CheckVersion(s, b.Version);
        if (b.Environment is not ("Homologacion" or "Produccion")) throw new DomainException("Elegí homologación o producción.");
        if (b.PointOfSale is < 1 or > 99998) throw new DomainException("Punto de venta inválido.");
        FiscalRules.VatId(b.DefaultVatRate);
        if (!string.IsNullOrWhiteSpace(b.CertificatePem) || !string.IsNullOrWhiteSpace(b.PrivateKeyPem))
        {
            if (string.IsNullOrWhiteSpace(b.CertificatePem) || string.IsNullOrWhiteSpace(b.PrivateKeyPem)) throw new DomainException("Subí el certificado (.crt) y la clave privada (.key) juntos.");
            try
            {
                using var cert = X509Certificate2.CreateFromPem(b.CertificatePem, b.PrivateKeyPem);
                if (!cert.HasPrivateKey) throw new DomainException("La clave privada no corresponde al certificado.");
                s.CertificateExpiresAtUtc = cert.NotAfter.ToUniversalTime();
            }
            catch (CryptographicException) { throw new DomainException("No se pudo leer el certificado o la clave. Deben estar en formato PEM."); }
            var p = protection.CreateProtector(Purpose);
            s.ProtectedCertificate = p.Protect(b.CertificatePem.Trim());
            s.ProtectedPrivateKey = p.Protect(b.PrivateKeyPem.Trim());
        }
        var profile = await ShopProvisioning.EnsureProfile(Db, Shop);
        if (b.Enabled && (!FiscalRules.ValidCuit(profile.TaxId) || s.ProtectedCertificate.Length == 0))
            throw new DomainException("Para activar ARCA cargá un CUIT válido en los datos del taller y el certificado.");
        s.Enabled = b.Enabled; s.Environment = b.Environment; s.PointOfSale = b.PointOfSale; s.DefaultVatRate = b.DefaultVatRate;
        await Db.SaveChangesAsync();
        return await GetSettings();
    }

    [HttpPost("fiscal/test")]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        RequireAdmin();
        var s = await Settings();
        var profile = await ShopProvisioning.EnsureProfile(Db, Shop);
        var provider = options.Value.Provider == "Arca" && s.ProtectedCertificate.Length > 0 ? (IFiscalProvider)arca : simulated;
        return Ok(new { data = new { message = await provider.TestAsync(s, profile.TaxId, ct) } });
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> List([FromQuery] string? sourceType, [FromQuery] Guid? sourceId, [FromQuery] int take = 200)
    {
        var q = Own<FiscalInvoice>();
        if (sourceId is { } id) q = q.Where(x => x.SourceId == id && (sourceType == null || x.SourceType == sourceType));
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 1000)).ToListAsync();
        return Ok(new { data = rows.Select(View) });
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var invoice = await Find<FiscalInvoice>(id);
        var profile = await ShopProvisioning.EnsureProfile(Db, Shop);
        return Ok(new { data = new { invoice = View(invoice, profile.TaxId), shop = new { profile.DisplayName, profile.LegalName, profile.TaxId, profile.TaxCondition, profile.Address, profile.City, profile.Phone, profile.Email, profile.LogoDataUrl, profile.PrimaryColor, profile.ReceiptFooter } } });
    }

    private static object View(FiscalInvoice x) => View(x, "");

    private static object View(FiscalInvoice x, string cuit) => new
    {
        x.Id, x.Version, x.CreatedAtUtc, x.SourceType, x.SourceId, x.VoucherType, VoucherName = FiscalRules.Name(x.VoucherType), x.PointOfSale, x.Number,
        Formatted = $"{x.PointOfSale:00000}-{x.Number:00000000}", x.Cae, x.CaeDueDate, x.CustomerName, x.DocType, x.DocNumber, x.CustomerTaxCondition,
        x.Net, x.Vat, x.Total, x.Currency, x.ExchangeRate, Lines = JsonSerializer.Deserialize<JsonElement>(x.LinesJson), x.Status, x.Environment, x.ProviderMessage, x.CancelsInvoiceId,
        // Data for the ARCA QR (RG 4892), rendered by the frontend.
        Qr = x.Status == "Authorized" && cuit.Length == 11 ? Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ver = 1, fecha = x.CreatedAtUtc.ToString("yyyy-MM-dd"), cuit = long.Parse(cuit), ptoVta = x.PointOfSale, tipoCmp = x.VoucherType, nroCmp = x.Number, importe = x.Total, moneda = x.Currency == "USD" ? "DOL" : "PES", ctz = x.ExchangeRate, tipoDocRec = x.DocType, nroDocRec = long.TryParse(x.DocNumber, out var d) ? d : 0, tipoCodAut = "E", codAut = long.TryParse(x.Cae, out var c) ? c : 0 }))) : "",
    };

    [HttpPost("invoices")]
    public Task<IActionResult> Issue(IssueInvoiceRequest b, CancellationToken ct) => Change($"invoice:{b.SourceType}:{b.SourceId}", b, async () =>
    {
        if (await Own<FiscalInvoice>().AnyAsync(x => x.SourceId == b.SourceId && (x.Status == "Authorized" || x.Status == "Simulated") && x.VoucherType != 3 && x.VoucherType != 8 && x.VoucherType != 13
                && !Own<FiscalInvoice>().Any(n => n.CancelsInvoiceId == x.Id && (n.Status == "Authorized" || n.Status == "Simulated"))))
            throw new DomainException("Ya hay un comprobante vigente para este origen. Anulalo con una nota de crédito antes de emitir otro.");
        var settings = await Settings();
        var profile = await ShopProvisioning.EnsureProfile(Db, Shop);
        List<FiscalLine> lines; decimal total; string currency; string concept; Guid? customerId; string customerName;
        if (b.SourceType == "Order")
        {
            var (w, o) = await LockOrder(b.SourceId);
            var quote = await Db.WorkflowQuotes.Where(x => x.ShopId == Shop && x.OrderId == w.Id && x.Status == "Accepted").OrderByDescending(x => x.Revision).FirstOrDefaultAsync()
                ?? throw new DomainException("La orden necesita un presupuesto aprobado para facturar.");
            var refunds = await Db.WorkflowRefunds.Where(x => x.ShopId == Shop && x.OrderId == w.Id).SumAsync(x => x.Amount);
            lines = (JsonSerializer.Deserialize<List<QuoteItem>>(quote.LinesJson, Saas.Json) ?? []).Select(l => new FiscalLine(l.Description, l.Quantity, l.UnitPrice)).ToList();
            if (refunds > 0) lines.Add(new FiscalLine("Bonificación por devolución", 1, -refunds));
            total = quote.Total - refunds; currency = quote.Currency; concept = "Services"; customerId = o.CustomerId; customerName = w.CustomerName;
        }
        else if (b.SourceType == "Sale")
        {
            var sale = await Find<CounterSale>(b.SourceId);
            if (sale.Status != "Completed") throw new DomainException("La venta está anulada.");
            lines = (JsonSerializer.Deserialize<List<SaleLine>>(sale.LinesJson, Saas.Json) ?? []).Select(l => new FiscalLine(l.Description, l.Quantity, l.UnitPrice)).ToList();
            if (sale.Discount > 0) lines.Add(new FiscalLine("Descuento", 1, -sale.Discount));
            total = sale.Total; currency = sale.Currency; concept = "Products"; customerId = sale.CustomerId; customerName = sale.CustomerName;
        }
        else throw new DomainException("Origen de comprobante inválido.");
        if (total <= 0) throw new DomainException("El total a facturar debe ser mayor a cero.");
        var contact = customerId is Guid cid ? await Own<CustomerContact>().SingleOrDefaultAsync(x => x.CustomerId == cid) : null;
        var docType = b.DocType ?? contact?.DocType ?? 99;
        var docNumber = new string((b.DocNumber ?? contact?.DocNumber ?? "").Where(char.IsDigit).ToArray());
        var condition = b.TaxCondition ?? contact?.TaxCondition ?? "ConsumidorFinal";
        if (docType is not (80 or 86 or 96 or 99)) throw new DomainException("Tipo de documento inválido.");
        if (docType is 80 or 86 && !FiscalRules.ValidCuit(docNumber)) throw new DomainException("El CUIT/CUIL del cliente no es válido.");
        if (docType == 96 && docNumber.Length is < 7 or > 8) throw new DomainException("El DNI debe tener 7 u 8 dígitos.");
        var type = FiscalRules.VoucherType(profile.TaxCondition, condition);
        if (type == 1 && docType != 80) throw new DomainException("La factura A requiere el CUIT del cliente.");
        // Remember the customer's fiscal data for next time.
        if (customerId is Guid known && (b.DocType is not null || b.TaxCondition is not null))
        {
            if (contact is null) { contact = new CustomerContact { ShopId = Shop, CustomerId = known }; Db.CustomerContacts.Add(contact); }
            contact.DocType = docType; contact.DocNumber = docNumber; contact.TaxCondition = condition;
        }
        var request = new FiscalRequest(profile.TaxId, profile.TaxCondition, settings.PointOfSale, type, concept, docType, docNumber, condition, total, settings.DefaultVatRate, currency, lines, null);
        var result = await Provider(settings).AuthorizeAsync(settings, request, ct);
        var invoice = Save(b.SourceType, b.SourceId, request, result, (b.CustomerName ?? "").Trim() is { Length: > 2 } n ? n : customerName, null);
        if (b.SourceType == "Sale" && result.Status != "Rejected") (await Find<CounterSale>(b.SourceId)).InvoiceId = invoice.Id;
        if (b.SourceType == "Order" && result.Status != "Rejected") Note(b.SourceId, $"{FiscalRules.Name(type)} {invoice.PointOfSale:00000}-{invoice.Number:00000000} {(result.Status == "Authorized" ? $"autorizada (CAE {result.Cae})" : "emitida como comprobante interno")}.");
        return View(invoice);
    });

    [HttpPost("invoices/{id:guid}/credit-note")]
    public Task<IActionResult> CreditNote(Guid id, CreditNoteRequest b, CancellationToken ct) => Change($"credit-note:{id}", b, async () =>
    {
        RequireAdmin();
        var original = await Find<FiscalInvoice>(id);
        if (FiscalRules.IsCreditNote(original.VoucherType) || original.Status == "Rejected") throw new DomainException("Solo se anulan facturas emitidas.");
        if (await Own<FiscalInvoice>().AnyAsync(x => x.CancelsInvoiceId == id && x.Status != "Rejected")) throw new DomainException("La factura ya fue anulada.");
        var settings = await Settings();
        var profile = await ShopProvisioning.EnsureProfile(Db, Shop);
        var type = original.VoucherType switch { 1 => 3, 6 => 8, _ => 13 };
        var lines = new List<FiscalLine> { new($"Anulación {FiscalRules.Name(original.VoucherType)} {original.PointOfSale:00000}-{original.Number:00000000}. {Saas.Text(b.Reason, "Motivo", 3, 200)}", 1, original.Total) };
        var request = new FiscalRequest(profile.TaxId, profile.TaxCondition, original.PointOfSale, type, original.SourceType == "Sale" ? "Products" : "Services",
            original.DocType, original.DocNumber, original.CustomerTaxCondition, original.Total, settings.DefaultVatRate, original.Currency, lines, original);
        var provider = original.Status == "Authorized" ? arca : (IFiscalProvider)simulated;
        var result = await provider.AuthorizeAsync(settings, request, ct);
        var note = Save(original.SourceType, original.SourceId, request, result, original.CustomerName, original.Id);
        if (original.SourceType == "Sale" && result.Status != "Rejected") (await Find<CounterSale>(original.SourceId)).InvoiceId = null;
        return View(note);
    });

    private FiscalInvoice Save(string sourceType, Guid sourceId, FiscalRequest r, FiscalResult result, string customerName, Guid? cancels)
    {
        var invoice = new FiscalInvoice
        {
            ShopId = Shop, SourceType = sourceType, SourceId = sourceId, VoucherType = r.VoucherType, PointOfSale = r.PointOfSale, Number = result.Number, Cae = result.Cae,
            CaeDueDate = result.CaeDueDate, CustomerName = customerName, DocType = r.DocType, DocNumber = r.DocType == 99 ? "0" : r.DocNumber, CustomerTaxCondition = r.ReceiverCondition,
            Net = result.Net, Vat = result.Vat, Total = r.Total, Currency = r.Currency, ExchangeRate = result.ExchangeRate, LinesJson = JsonSerializer.Serialize(r.Lines, Saas.Json),
            Status = result.Status, Environment = result.Environment, ProviderMessage = result.Message.Length > 1900 ? result.Message[..1900] : result.Message, CancelsInvoiceId = cancels, ActorId = Actor,
        };
        Db.FiscalInvoices.Add(invoice);
        if (result.Status != "Rejected") Webhooks.Enqueue(Db, Shop, "invoice.issued", new { invoice.Id, invoice.VoucherType, invoice.Number, invoice.Total, invoice.Currency, invoice.Status });
        return invoice;
    }
}
