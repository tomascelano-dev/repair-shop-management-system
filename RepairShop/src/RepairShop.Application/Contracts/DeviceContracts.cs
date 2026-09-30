using System.ComponentModel.DataAnnotations;

namespace RepairShop.Application.Contracts;

public sealed record DeviceResponse(
    Guid Id,
    Guid ShopId,
    Guid CustomerId,
    string Brand,
    string Model,
    string? Label,
    string? SerialNumber,
    string? Notes,
    DateTime CreatedAtUtc
);

public sealed record DeviceCreateRequest(
    [Required] Guid CustomerId,
    [Required, MinLength(2)] string Brand,
    [Required, MinLength(2)] string Model,
    string? Label,
    string? SerialNumber,
    string? Notes
);

public sealed record DeviceUpdateRequest(
    [Required, MinLength(2)] string Brand,
    [Required, MinLength(2)] string Model,
    string? Label,
    string? SerialNumber,
    string? Notes
);
