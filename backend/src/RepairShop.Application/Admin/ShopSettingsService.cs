using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Billing;
using RepairShop.Application.Common;
using RepairShop.Application.Contracts;
using RepairShop.Domain.Auditing;
using RepairShop.Domain.Common;
using RepairShop.Domain.Inventory;
using RepairShop.Domain.Messaging;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;

namespace RepairShop.Application.Admin;

public sealed class ShopSettingsService
{
    private const string EntityType = "shop";

    private readonly IShopRepository _shops;
    private readonly IShopIntegrationRepository _integrations;
    private readonly IUserShopAccessRepository _access;
    private readonly IUserRepository _users;
    private readonly IMessageTemplateRepository _templates;
    private readonly IInventoryItemRepository _items;
    private readonly IAuditEventRepository _audit;
    private readonly ISecretProtector _protector;
    private readonly IIntegrationStatus _status;
    private readonly IFileUrlSigner _fileUrls;
    private readonly IAppLinks _links;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly SubscriptionService _subscriptions;

    public ShopSettingsService(
        IShopRepository shops,
        IShopIntegrationRepository integrations,
        IUserShopAccessRepository access,
        IUserRepository users,
        IMessageTemplateRepository templates,
        IInventoryItemRepository items,
        IAuditEventRepository audit,
        ISecretProtector protector,
        IIntegrationStatus status,
        IFileUrlSigner fileUrls,
        IAppLinks links,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        SubscriptionService subscriptions)
    {
        _shops = shops;
        _integrations = integrations;
        _access = access;
        _users = users;
        _templates = templates;
        _items = items;
        _audit = audit;
        _protector = protector;
        _status = status;
        _fileUrls = fileUrls;
        _links = links;
        _uow = uow;
        _clock = clock;
        _subscriptions = subscriptions;
    }

    public async Task<ShopSettingsResponse> GetAsync(Guid shopId, CancellationToken ct)
        => ToResponse(await GetShopAsync(shopId, ct));

    public async Task<ShopSettingsResponse> UpdateAsync(Guid shopId, UpdateShopSettingsRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var shop = await GetShopAsync(shopId, ct);

        if (!IsValidTimeZone(req.TimeZone)) throw new DomainException("Zona horaria inválida (usá un id IANA, ej: America/Argentina/Buenos_Aires).");
        if (!Enum.IsDefined(req.DefaultNotificationChannel)) throw new DomainException("Canal de notificación inválido.");

        shop.Update(req.Name, req.Phone, req.AddressLine, req.City, req.Country, now);
        shop.UpdateBusiness(req.LegalName, req.TaxId, req.TaxCondition, req.Email, now);
        shop.UpdateRegional(req.DefaultCurrency, req.ReportingCurrency, req.PhoneCountryCode, req.TimeZone, now);
        shop.UpdateOperations(req.DefaultWarrantyDays, req.QuoteValidityDays, req.ReceptionTerms, req.WarrantyTerms, req.PickupHours,
            req.GoogleReviewUrl, req.ReadyReminderDays, req.StaleOrderDays, req.RequireOpenCashSession, now);
        shop.UpdateNotifications(req.NotificationsEnabled, req.DefaultNotificationChannel, req.SendFeedbackSurvey, now);

        await AuditAsync(shopId, "shop_settings_updated", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(shop);
    }

    public async Task<ShopSettingsResponse> SetLogoAsync(Guid shopId, Guid? fileId, Actor actor, CancellationToken ct)
    {
        var shop = await GetShopAsync(shopId, ct);
        shop.SetLogo(fileId, _clock.UtcNow);
        await AuditAsync(shopId, "shop_logo_updated", actor, null, ct);
        await _uow.SaveChangesAsync(ct);
        return ToResponse(shop);
    }

    public async Task<IntegrationsStatusResponse> GetIntegrationsAsync(Guid shopId, CancellationToken ct)
    {
        var integration = await _integrations.GetAsync(shopId, ct);
        return new IntegrationsStatusResponse(
            integration?.MercadoPagoEnabled ?? false,
            integration?.MercadoPagoAccessTokenProtected is not null,
            integration?.MercadoPagoWebhookSecretProtected is not null,
            $"{_links.ApiBase.TrimEnd('/')}/api/v1/webhooks/mercadopago/{shopId}",
            integration?.FiscalEnabled ?? false,
            integration?.FiscalEnvironment ?? FiscalEnvironment.Homologacion,
            integration?.FiscalPointOfSale ?? 0,
            integration?.FiscalCertificateProtected is not null,
            _status.StorageProvider,
            Enum.GetValues<NotificationChannel>().ToDictionary(c => c.ToString(), c => _status.IsChannelConfigured(c)),
            _status.AiConfigured);
    }

    public async Task<IntegrationsStatusResponse> UpdateMercadoPagoAsync(Guid shopId, UpdateMercadoPagoRequest req, Actor actor, CancellationToken ct)
    {
        var integration = await GetOrCreateIntegrationAsync(shopId, ct);
        integration.ConfigureMercadoPago(
            req.Enabled,
            req.AccessToken is null ? null : req.AccessToken.Trim().Length == 0 ? "" : _protector.Protect(req.AccessToken.Trim()),
            req.WebhookSecret is null ? null : req.WebhookSecret.Trim().Length == 0 ? "" : _protector.Protect(req.WebhookSecret.Trim()),
            _clock.UtcNow);

        await AuditAsync(shopId, "integration_mercadopago_updated", actor, new { req.Enabled }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetIntegrationsAsync(shopId, ct);
    }

    public async Task<IntegrationsStatusResponse> UpdateFiscalAsync(Guid shopId, UpdateFiscalRequest req, Actor actor, CancellationToken ct)
    {
        var shop = await GetShopAsync(shopId, ct);
        if (req.Enabled && string.IsNullOrWhiteSpace(shop.TaxId)) throw new DomainException("Cargá el CUIT de la sucursal antes de habilitar la facturación.");

        string? certProtected = null, passwordProtected = null;
        if (req.CertificatePfxBase64 is not null)
        {
            if (req.CertificatePfxBase64.Trim().Length == 0)
            {
                certProtected = "";
                passwordProtected = "";
            }
            else
            {
                byte[] pfx;
                try { pfx = Convert.FromBase64String(req.CertificatePfxBase64.Trim()); }
                catch (FormatException) { throw new DomainException("El certificado debe estar en base64 (archivo .pfx/.p12)."); }
                ValidatePfx(pfx, req.CertificatePassword);
                certProtected = _protector.Protect(req.CertificatePfxBase64.Trim());
                passwordProtected = _protector.Protect(req.CertificatePassword ?? "");
            }
        }

        var integration = await GetOrCreateIntegrationAsync(shopId, ct);
        integration.ConfigureFiscal(req.Enabled, req.Environment, req.PointOfSale, certProtected, passwordProtected, _clock.UtcNow);

        await AuditAsync(shopId, "integration_fiscal_updated", actor, new { req.Enabled, environment = req.Environment.ToString(), req.PointOfSale }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetIntegrationsAsync(shopId, ct);
    }

    // ===== Branches (multi-sucursal) =====

    public async Task<List<BranchResponse>> ListBranchesAsync(Guid currentShopId, Guid userId, CancellationToken ct)
    {
        var current = await GetShopAsync(currentShopId, ct);
        var user = await _users.GetByIdAsync(userId, ct);
        var accesses = user is null ? new List<UserShopAccess>() : await _access.ListByUserAsync(userId, ct);
        var branches = await _shops.ListByOrganizationAsync(current.OrganizationId, ct);
        return branches.Select(s =>
        {
            string? role = user is null ? null
                : s.Id == user.ShopId ? user.Role.ToString()
                : accesses.FirstOrDefault(a => a.ShopId == s.Id)?.Role.ToString();
            return new BranchResponse(s.Id, s.Name, s.City, s.AddressLine, s.IsActive, s.Id == currentShopId, role);
        }).ToList();
    }

    public async Task<BranchResponse> CreateBranchAsync(Guid currentShopId, CreateBranchRequest req, Actor actor, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var current = await GetShopAsync(currentShopId, ct);
        await _subscriptions.EnsureBranchCapacityAsync(current.OrganizationId, ct);

        var branch = new Shop(current.OrganizationId, req.Name, req.Phone, req.AddressLine, req.City, current.Country, now);
        branch.UpdateBusiness(current.LegalName, current.TaxId, current.TaxCondition, current.Email, now);
        branch.UpdateRegional(current.DefaultCurrency, current.ReportingCurrency, current.PhoneCountryCode, current.TimeZone, now);
        branch.UpdateOperations(current.DefaultWarrantyDays, current.QuoteValidityDays, current.ReceptionTerms, current.WarrantyTerms,
            current.PickupHours, current.GoogleReviewUrl, current.ReadyReminderDays, current.StaleOrderDays, current.RequireOpenCashSession, now);
        branch.UpdateNotifications(current.NotificationsEnabled, current.DefaultNotificationChannel, current.SendFeedbackSurvey, now);
        await _shops.AddAsync(branch, ct);

        // The creator administers the new branch.
        await _access.AddAsync(new UserShopAccess(actor.UserId, branch.Id, UserRole.Admin, now), ct);

        if (req.CopyTemplates)
        {
            foreach (var t in await _templates.ListAsync(currentShopId, includeInactive: true, ct))
                await _templates.AddAsync(new MessageTemplate(branch.Id, t.Key, t.Title, t.Body, t.IsActive, now), ct);
        }

        if (req.CopyCatalog)
        {
            // Page through the whole catalog (the repository caps each page at 200 rows).
            const int pageSize = 200;
            for (var skip = 0; ; skip += pageSize)
            {
                var (items, _) = await _items.SearchAsync(currentShopId, new InventorySearchOptions(IncludeInactive: false, SortBy: "sku", Skip: skip, Take: pageSize), ct);
                foreach (var i in items)
                {
                    var copy = new InventoryItem(branch.Id, i.Sku, i.Name, 0, i.UnitCost, i.UnitCostCurrency, true, now);
                    copy.UpdateCatalog(i.Category, i.Barcode, i.MinStock, i.TrackStock, i.IsSellable, i.SalePrice, i.SalePriceCurrency, i.WarrantyDays, i.Location, now);
                    await _items.AddAsync(copy, ct);
                }
                if (items.Count < pageSize) break;
            }
        }

        await AuditAsync(currentShopId, "branch_created", actor, new { branchId = branch.Id, branch.Name }, ct);
        await _uow.SaveChangesAsync(ct);
        return new BranchResponse(branch.Id, branch.Name, branch.City, branch.AddressLine, branch.IsActive, false, UserRole.Admin.ToString());
    }

    public async Task SetBranchActiveAsync(Guid currentShopId, Guid branchId, bool isActive, Actor actor, CancellationToken ct)
    {
        var current = await GetShopAsync(currentShopId, ct);
        var branch = await GetShopAsync(branchId, ct);
        if (branch.OrganizationId != current.OrganizationId) throw new ForbiddenException("La sucursal pertenece a otra organización.");
        if (branch.Id == currentShopId && !isActive) throw new DomainException("No podés desactivar la sucursal en la que estás trabajando.");
        if (isActive && !branch.IsActive) await _subscriptions.EnsureBranchCapacityAsync(current.OrganizationId, ct);
        branch.SetActive(isActive, _clock.UtcNow);
        await AuditAsync(currentShopId, isActive ? "branch_activated" : "branch_deactivated", actor, new { branchId }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<Shop> GetShopAsync(Guid shopId, CancellationToken ct)
        => await _shops.GetByIdAsync(shopId, ct) ?? throw new NotFoundException("Sucursal no encontrada.");

    private async Task<ShopIntegration> GetOrCreateIntegrationAsync(Guid shopId, CancellationToken ct)
    {
        var integration = await _integrations.GetAsync(shopId, ct);
        if (integration is not null) return integration;
        integration = new ShopIntegration(shopId, _clock.UtcNow);
        await _integrations.AddAsync(integration, ct);
        return integration;
    }

    private static void ValidatePfx(byte[] pfx, string? password)
    {
        try
        {
            using var cert = new X509Certificate2(pfx, password, X509KeyStorageFlags.EphemeralKeySet);
            if (!cert.HasPrivateKey) throw new DomainException("El certificado no incluye la clave privada.");
            if (cert.NotAfter < DateTime.UtcNow) throw new DomainException("El certificado está vencido.");
        }
        catch (DomainException) { throw; }
        catch (Exception) { throw new DomainException("No se pudo abrir el certificado: revisá el archivo y la contraseña."); }
    }

    private static bool IsValidTimeZone(string tz)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(tz); return true; }
        catch { return false; }
    }

    private ShopSettingsResponse ToResponse(Shop s)
        => new(s.Id, s.OrganizationId, s.Name, s.Phone, s.AddressLine, s.City, s.Country, s.LegalName, s.TaxId, s.TaxCondition, s.Email,
            s.LogoFileId, s.LogoFileId is null ? null : _fileUrls.GetUrl(s.LogoFileId.Value, TimeSpan.FromHours(12)),
            s.DefaultCurrency, s.ReportingCurrency, s.PhoneCountryCode, s.TimeZone, s.DefaultWarrantyDays, s.QuoteValidityDays,
            s.ReceptionTerms, s.WarrantyTerms, s.PickupHours, s.GoogleReviewUrl, s.ReadyReminderDays, s.StaleOrderDays,
            s.NotificationsEnabled, s.DefaultNotificationChannel, s.SendFeedbackSurvey, s.RequireOpenCashSession, s.IsActive);

    private Task AuditAsync(Guid shopId, string action, Actor actor, object? data, CancellationToken ct)
        => _audit.AddAsync(new AuditEvent(shopId, EntityType, shopId, action, actor.UserId, actor.Email,
            data is null ? null : JsonSerializer.Serialize(data), _clock.UtcNow), ct);
}
