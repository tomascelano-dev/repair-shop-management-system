using System.ComponentModel.DataAnnotations;

namespace RepairShop.Application.Contracts;

public sealed record CustomerResponse(
    Guid Id,
    Guid ShopId,
    string FullName,
    string Phone,
    string? Notes,
    DateTime CreatedAtUtc
);

public sealed record CustomerCreateRequest(
    [Required, MinLength(3)] string FullName,
    [Required, MinLength(6)] string Phone,
    string? Notes
);

public sealed record CustomerUpdateRequest(
    [Required, MinLength(3)] string FullName,
    [Required, MinLength(6)] string Phone,
    string? Notes
);
