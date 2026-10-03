using System.ComponentModel.DataAnnotations;
using RepairShop.Domain.Customers;
using RepairShop.Domain.Notifications;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;

namespace RepairShop.Application.Contracts;

public sealed record UserAdminResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    bool IsHomeShop,
    bool HasPendingInvitation,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<ShopAccessResponse> Shops);

public sealed record CreateUserRequest(
    [Required] string Email,
    [Required, MinLength(2)] string DisplayName,
    [Required] UserRole Role);

public sealed record UpdateUserRequest(
    [Required, MinLength(2)] string DisplayName,
    [Required] UserRole Role,
    bool IsActive);

public sealed record UserLinkResponse(UserAdminResponse User, string Url, DateTime ExpiresAtUtc);

public sealed record GrantShopAccessRequest([Required] Guid ShopId, [Required] UserRole Role);

public sealed record ShopSettingsResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Phone,
    string? AddressLine,
    string? City,
    string? Country,
    string? LegalName,
    string? TaxId,
    CustomerTaxCondition TaxCondition,
    string? Email,
    Guid? LogoFileId,
    string? LogoUrl,
    string DefaultCurrency,
    string ReportingCurrency,
    string PhoneCountryCode,
    string TimeZone,
    int DefaultWarrantyDays,
    int QuoteValidityDays,
    string? ReceptionTerms,
    string? WarrantyTerms,
    string? PickupHours,
    string? GoogleReviewUrl,
    string ReadyReminderDays,
    int StaleOrderDays,
    bool NotificationsEnabled,
    NotificationChannel DefaultNotificationChannel,
    bool SendFeedbackSurvey,
    bool RequireOpenCashSession,
    bool IsActive);

public sealed record UpdateShopSettingsRequest(
    [Required, MinLength(2)] string Name,
    string? Phone,
    string? AddressLine,
    string? City,
    string? Country,
    string? LegalName,
    string? TaxId,
    CustomerTaxCondition TaxCondition,
    string? Email,
    [Required] string DefaultCurrency,
    [Required] string ReportingCurrency,
    [Required] string PhoneCountryCode,
    [Required] string TimeZone,
    int DefaultWarrantyDays,
    int QuoteValidityDays,
    string? ReceptionTerms,
    string? WarrantyTerms,
    string? PickupHours,
    string? GoogleReviewUrl,
    string? ReadyReminderDays,
    int StaleOrderDays,
    bool NotificationsEnabled,
    NotificationChannel DefaultNotificationChannel,
    bool SendFeedbackSurvey,
    bool RequireOpenCashSession);

public sealed record IntegrationsStatusResponse(
    bool MercadoPagoEnabled,
    bool MercadoPagoAccessTokenConfigured,
    bool MercadoPagoWebhookSecretConfigured,
    string MercadoPagoWebhookUrl,
    bool FiscalEnabled,
    FiscalEnvironment FiscalEnvironment,
    int FiscalPointOfSale,
    bool FiscalCertificateConfigured,
    string StorageProvider,
    IReadOnlyDictionary<string, bool> NotificationChannels,
    bool AiConfigured);

public sealed record UpdateMercadoPagoRequest(bool Enabled, string? AccessToken, string? WebhookSecret);

public sealed record UpdateFiscalRequest(bool Enabled, FiscalEnvironment Environment, int PointOfSale, string? CertificatePfxBase64, string? CertificatePassword);

public sealed record BranchResponse(Guid Id, string Name, string? City, string? AddressLine, bool IsActive, bool IsCurrent, string? MyRole);

public sealed record CreateBranchRequest([Required, MinLength(2)] string Name, string? Phone, string? AddressLine, string? City, bool CopyTemplates = true, bool CopyCatalog = false);
