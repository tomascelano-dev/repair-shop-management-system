using RepairShop.Domain.Common;

namespace RepairShop.Domain.Shops;

public enum FiscalEnvironment
{
    Homologacion = 0,
    Produccion = 1
}

/// <summary>
/// Per-shop credentials for external integrations. Secrets are stored encrypted (Data Protection)
/// and never returned by the API.
/// </summary>
public sealed class ShopIntegration
{
    public Guid ShopId { get; private set; }

    // Mercado Pago (Checkout Pro)
    public bool MercadoPagoEnabled { get; private set; }
    public string? MercadoPagoAccessTokenProtected { get; private set; }
    public string? MercadoPagoWebhookSecretProtected { get; private set; }

    // ARCA electronic invoicing (WSAA + WSFEv1)
    public bool FiscalEnabled { get; private set; }
    public FiscalEnvironment FiscalEnvironment { get; private set; } = FiscalEnvironment.Homologacion;
    public int FiscalPointOfSale { get; private set; }
    public string? FiscalCertificateProtected { get; private set; }   // PFX base64 (encrypted)
    public string? FiscalCertificatePasswordProtected { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private ShopIntegration() { }

    public ShopIntegration(Guid shopId, DateTime nowUtc)
    {
        if (shopId == Guid.Empty) throw new DomainException("Falta la sucursal.");
        ShopId = shopId;
        UpdatedAtUtc = nowUtc;
    }

    public void ConfigureMercadoPago(bool enabled, string? accessTokenProtected, string? webhookSecretProtected, DateTime nowUtc)
    {
        if (accessTokenProtected is not null) MercadoPagoAccessTokenProtected = accessTokenProtected.Length == 0 ? null : accessTokenProtected;
        if (webhookSecretProtected is not null) MercadoPagoWebhookSecretProtected = webhookSecretProtected.Length == 0 ? null : webhookSecretProtected;
        if (enabled && MercadoPagoAccessTokenProtected is null) throw new DomainException("Para habilitar Mercado Pago cargá el access token.");
        MercadoPagoEnabled = enabled;
        UpdatedAtUtc = nowUtc;
    }

    public void ConfigureFiscal(bool enabled, FiscalEnvironment environment, int pointOfSale, string? certificateProtected, string? passwordProtected, DateTime nowUtc)
    {
        if (pointOfSale is < 0 or > 99999) throw new DomainException("Punto de venta inválido.");
        if (certificateProtected is not null) FiscalCertificateProtected = certificateProtected.Length == 0 ? null : certificateProtected;
        if (passwordProtected is not null) FiscalCertificatePasswordProtected = passwordProtected.Length == 0 ? null : passwordProtected;
        if (enabled && (FiscalCertificateProtected is null || pointOfSale == 0))
            throw new DomainException("Para habilitar la facturación cargá el certificado y el punto de venta.");

        FiscalEnabled = enabled;
        FiscalEnvironment = environment;
        FiscalPointOfSale = pointOfSale;
        UpdatedAtUtc = nowUtc;
    }
}
