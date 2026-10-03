using System.ComponentModel.DataAnnotations;
using RepairShop.Domain.Customers;

namespace RepairShop.Application.Contracts;

public sealed record CustomerResponse(
    Guid Id,
    Guid ShopId,
    string FullName,
    string Phone,
    string? Notes,
    DateTime CreatedAtUtc,
    string? Email = null,
    CustomerDocumentType DocumentType = CustomerDocumentType.None,
    string? DocumentNumber = null,
    CustomerTaxCondition TaxCondition = CustomerTaxCondition.ConsumidorFinal,
    string? Address = null,
    string? Tags = null,
    bool NotificationsOptIn = true,
    bool MarketingOptIn = false,
    DateTime? UpdatedAtUtc = null,
    string? WhatsAppNumber = null
);

public sealed record CustomerCreateRequest(
    [Required, MinLength(3)] string FullName,
    [Required, MinLength(6)] string Phone,
    string? Notes,
    string? Email = null,
    CustomerDocumentType DocumentType = CustomerDocumentType.None,
    string? DocumentNumber = null,
    CustomerTaxCondition TaxCondition = CustomerTaxCondition.ConsumidorFinal,
    string? Address = null,
    string? Tags = null,
    bool NotificationsOptIn = true,
    bool MarketingOptIn = false
);

public sealed record CustomerUpdateRequest(
    [Required, MinLength(3)] string FullName,
    [Required, MinLength(6)] string Phone,
    string? Notes,
    string? Email = null,
    CustomerDocumentType DocumentType = CustomerDocumentType.None,
    string? DocumentNumber = null,
    CustomerTaxCondition TaxCondition = CustomerTaxCondition.ConsumidorFinal,
    string? Address = null,
    string? Tags = null,
    bool NotificationsOptIn = true,
    bool MarketingOptIn = false
);

public sealed record DuplicateCustomerResponse(Guid Id, string FullName, string Phone, string? Email, DateTime CreatedAtUtc, int Devices, int Orders);

public sealed record MergeCustomersRequest([Required] Guid SourceCustomerId);

public sealed record CustomerSummaryResponse(
    CustomerResponse Customer,
    IReadOnlyList<DeviceResponse> Devices,
    IReadOnlyList<CustomerOrderItem> Orders,
    IReadOnlyList<CustomerSaleItem> Sales,
    IReadOnlyList<CurrencyAmount> TotalSpent,
    IReadOnlyList<CurrencyAmount> BalanceDue,
    int OpenOrders,
    DateTime? LastVisitAtUtc,
    double? AverageFeedbackScore);

public sealed record CustomerOrderItem(Guid Id, string Code, string Status, string DeviceLabel, string IssueDescription, decimal Total, decimal Balance, string Currency, DateTime CreatedAtUtc, DateTime? DeliveredAtUtc);

public sealed record CustomerSaleItem(Guid Id, string Code, string Status, decimal Total, string Currency, DateTime CreatedAtUtc);

public sealed record CurrencyAmount(string Currency, decimal Amount);
