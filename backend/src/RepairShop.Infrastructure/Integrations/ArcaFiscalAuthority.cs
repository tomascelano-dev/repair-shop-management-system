using System.Globalization;
using System.Net;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RepairShop.Application.Abstractions;
using RepairShop.Domain.Shops;

namespace RepairShop.Infrastructure.Integrations;

/// <summary>
/// ARCA (ex AFIP) electronic invoicing: WSAA authentication (CMS-signed login ticket) + WSFEv1 (CAE request).
/// Test it first against "homologación" with a testing certificate.
/// </summary>
public sealed class ArcaFiscalAuthority : IFiscalAuthority
{
    public const string HttpClientName = "arca";

    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Fe = "http://ar.gov.afip.dif.FEV1/";
    private static readonly XNamespace Wsaa = "http://wsaa.view.sua.dvadac.desein.afip.gov";

    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ArcaFiscalAuthority> _logger;

    public ArcaFiscalAuthority(IHttpClientFactory http, IMemoryCache cache, ILogger<ArcaFiscalAuthority> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<FiscalAuthorizationResult> AuthorizeAsync(FiscalCredentials credentials, FiscalVoucherRequest voucher, CancellationToken ct)
    {
        var (token, sign) = await LoginAsync(credentials, ct);

        // The number must be exactly last+1: retry once if another invoice took it meanwhile.
        for (var attempt = 1; ; attempt++)
        {
            var last = await LastAuthorizedAsync(credentials, token, sign, (int)voucher.Type, ct);
            var xml = BuildCaeRequest(token, sign, credentials.Cuit, credentials.PointOfSale, voucher, last + 1);
            var response = await PostAsync(WsfeUrl(credentials.Environment), "http://ar.gov.afip.dif.FEV1/FECAESolicitar", xml, ct);
            var result = ParseCaeResponse(response, last + 1);
            if (!result.Approved && attempt == 1 && result.Message.Contains("10016", StringComparison.Ordinal)) continue;
            return result;
        }
    }

    private async Task<(string Token, string Sign)> LoginAsync(FiscalCredentials c, CancellationToken ct)
    {
        var key = $"arca:ta:{c.Cuit}:{c.Environment}";
        if (_cache.TryGetValue<(string, string)>(key, out var cached)) return cached;

        using var cert = new X509Certificate2(c.CertificatePfx, c.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
        var cms = SignTicketRequest(BuildLoginTicketRequest(DateTimeOffset.UtcNow, "wsfe"), cert);
        var envelope = new XDocument(new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", Soap), new XAttribute(XNamespace.Xmlns + "wsaa", Wsaa),
            new XElement(Soap + "Header"),
            new XElement(Soap + "Body", new XElement(Wsaa + "loginCms", new XElement(Wsaa + "in0", cms)))));

        var response = await PostAsync(WsaaUrl(c.Environment), "", envelope.ToString(SaveOptions.DisableFormatting), ct);
        var (token, sign, expires) = ParseLoginResponse(response);
        _cache.Set(key, (token, sign), expires.AddMinutes(-10));
        return (token, sign);
    }

    private async Task<long> LastAuthorizedAsync(FiscalCredentials c, string token, string sign, int voucherType, CancellationToken ct)
    {
        var body = new XElement(Fe + "FECompUltimoAutorizado",
            Auth(token, sign, c.Cuit),
            new XElement(Fe + "PtoVta", c.PointOfSale),
            new XElement(Fe + "CbteTipo", voucherType));
        var response = await PostAsync(WsfeUrl(c.Environment), "http://ar.gov.afip.dif.FEV1/FECompUltimoAutorizado", Envelope(body), ct);

        var doc = XDocument.Parse(response);
        var errors = Errors(doc);
        if (errors.Length > 0) throw new InvalidOperationException($"ARCA: {errors}");
        return long.Parse(doc.Descendants(Fe + "CbteNro").First().Value, CultureInfo.InvariantCulture);
    }

    private async Task<string> PostAsync(string url, string soapAction, string xml, CancellationToken ct)
    {
        var client = _http.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(xml, Encoding.UTF8, "text/xml") };
        req.Headers.TryAddWithoutValidation("SOAPAction", soapAction);
        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode && res.StatusCode != HttpStatusCode.InternalServerError)
            throw new InvalidOperationException($"HTTP {(int)res.StatusCode}");

        var fault = XDocument.Parse(body).Descendants("faultstring").FirstOrDefault()?.Value;
        if (fault is not null)
        {
            _logger.LogWarning("ARCA SOAP fault: {Fault}", fault);
            throw new InvalidOperationException(fault);
        }

        return body;
    }

    // ===== Pure builders / parsers (unit tested) =====

    public static string BuildLoginTicketRequest(DateTimeOffset now, string service)
    {
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement("loginTicketRequest", new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", now.ToUnixTimeSeconds()),
                    new XElement("generationTime", now.AddMinutes(-10).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)),
                    new XElement("expirationTime", now.AddMinutes(10).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture))),
                new XElement("service", service)));
        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    public static string SignTicketRequest(string tra, X509Certificate2 certificate)
    {
        var content = new ContentInfo(Encoding.UTF8.GetBytes(tra));
        var signed = new SignedCms(content);
        signed.ComputeSignature(new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate) { IncludeOption = X509IncludeOption.EndCertOnly });
        return Convert.ToBase64String(signed.Encode());
    }

    public static (string Token, string Sign, DateTime ExpiresAtUtc) ParseLoginResponse(string soapResponse)
    {
        var envelope = XDocument.Parse(soapResponse);
        var inner = envelope.Descendants().FirstOrDefault(e => e.Name.LocalName == "loginCmsReturn")?.Value
                    ?? throw new InvalidOperationException("ARCA WSAA: respuesta inesperada.");
        var ticket = XDocument.Parse(inner);
        var token = ticket.Descendants("token").First().Value;
        var sign = ticket.Descendants("sign").First().Value;
        var expires = DateTimeOffset.Parse(ticket.Descendants("expirationTime").First().Value, CultureInfo.InvariantCulture).UtcDateTime;
        return (token, sign, expires);
    }

    public static string BuildCaeRequest(string token, string sign, string cuit, int pointOfSale, FiscalVoucherRequest v, long number)
    {
        static string Money(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);
        static string Date(DateOnly d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var detail = new XElement(Fe + "FECAEDetRequest",
            new XElement(Fe + "Concepto", v.Concept),
            new XElement(Fe + "DocTipo", v.DocumentType),
            new XElement(Fe + "DocNro", v.DocumentNumber),
            new XElement(Fe + "CbteDesde", number),
            new XElement(Fe + "CbteHasta", number),
            new XElement(Fe + "CbteFch", Date(v.Date)),
            new XElement(Fe + "ImpTotal", Money(v.Total)),
            new XElement(Fe + "ImpTotConc", "0.00"),
            new XElement(Fe + "ImpNeto", Money(v.NetAmount)),
            new XElement(Fe + "ImpOpEx", "0.00"),
            new XElement(Fe + "ImpTrib", "0.00"),
            new XElement(Fe + "ImpIVA", Money(v.VatAmount)));

        if (v.Concept != 1)
        {
            detail.Add(new XElement(Fe + "FchServDesde", Date(v.ServiceFrom ?? v.Date)));
            detail.Add(new XElement(Fe + "FchServHasta", Date(v.ServiceTo ?? v.Date)));
            detail.Add(new XElement(Fe + "FchVtoPago", Date(v.PaymentDue ?? v.Date)));
        }

        detail.Add(new XElement(Fe + "MonId", "PES"));
        detail.Add(new XElement(Fe + "MonCotiz", "1"));
        detail.Add(new XElement(Fe + "CondicionIVAReceptorId", v.ReceiverIvaConditionId));

        if (v.AssociatedType is not null && v.AssociatedNumber is not null)
        {
            detail.Add(new XElement(Fe + "CbtesAsoc",
                new XElement(Fe + "CbteAsoc",
                    new XElement(Fe + "Tipo", (int)v.AssociatedType.Value),
                    new XElement(Fe + "PtoVta", v.AssociatedPointOfSale ?? pointOfSale),
                    new XElement(Fe + "Nro", v.AssociatedNumber.Value))));
        }

        if (v.VatLines.Count > 0)
        {
            detail.Add(new XElement(Fe + "Iva", v.VatLines.Select(l =>
                new XElement(Fe + "AlicIva",
                    new XElement(Fe + "Id", l.VatId),
                    new XElement(Fe + "BaseImp", Money(l.BaseAmount)),
                    new XElement(Fe + "Importe", Money(l.Amount))))));
        }

        var body = new XElement(Fe + "FECAESolicitar",
            Auth(token, sign, cuit),
            new XElement(Fe + "FeCAEReq",
                new XElement(Fe + "FeCabReq",
                    new XElement(Fe + "CantReg", 1),
                    new XElement(Fe + "PtoVta", pointOfSale),
                    new XElement(Fe + "CbteTipo", (int)v.Type)),
                new XElement(Fe + "FeDetReq", detail)));

        return Envelope(body);
    }

    public static FiscalAuthorizationResult ParseCaeResponse(string soapResponse, long number)
    {
        var doc = XDocument.Parse(soapResponse);
        var det = doc.Descendants(Fe + "FECAEDetResponse").FirstOrDefault();
        var result = det?.Element(Fe + "Resultado")?.Value ?? doc.Descendants(Fe + "Resultado").FirstOrDefault()?.Value ?? "R";
        var cae = det?.Element(Fe + "CAE")?.Value;
        var dueText = det?.Element(Fe + "CAEFchVto")?.Value;

        var messages = new List<string>();
        var errors = Errors(doc);
        if (errors.Length > 0) messages.Add(errors);
        var obs = det?.Descendants(Fe + "Obs").Select(o => $"{o.Element(Fe + "Code")?.Value}: {o.Element(Fe + "Msg")?.Value}").ToList() ?? new List<string>();
        if (obs.Count > 0) messages.Add("Observaciones: " + string.Join(" | ", obs));

        var approved = result == "A" && !string.IsNullOrWhiteSpace(cae);
        DateOnly? due = dueText is { Length: 8 } ? DateOnly.ParseExact(dueText, "yyyyMMdd", CultureInfo.InvariantCulture) : null;
        return new FiscalAuthorizationResult(approved, number, approved ? cae : null, approved ? due : null,
            messages.Count == 0 ? (approved ? "Autorizado" : "Rechazado") : string.Join(" ", messages));
    }

    private static string Errors(XDocument doc)
        => string.Join(" | ", doc.Descendants(Fe + "Err").Select(e => $"{e.Element(Fe + "Code")?.Value}: {e.Element(Fe + "Msg")?.Value}"));

    private static XElement Auth(string token, string sign, string cuit)
        => new(Fe + "Auth", new XElement(Fe + "Token", token), new XElement(Fe + "Sign", sign), new XElement(Fe + "Cuit", cuit));

    private static string Envelope(XElement body)
        => new XDocument(new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", Soap), new XAttribute(XNamespace.Xmlns + "ar", Fe),
            new XElement(Soap + "Header"),
            new XElement(Soap + "Body", body))).ToString(SaveOptions.DisableFormatting);

    private static string WsaaUrl(FiscalEnvironment env) => env == FiscalEnvironment.Produccion
        ? "https://wsaa.afip.gov.ar/ws/services/LoginCms"
        : "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";

    private static string WsfeUrl(FiscalEnvironment env) => env == FiscalEnvironment.Produccion
        ? "https://servicios1.afip.gov.ar/wsfev1/service.asmx"
        : "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";
}
