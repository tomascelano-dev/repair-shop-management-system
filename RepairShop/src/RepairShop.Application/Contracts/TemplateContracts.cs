using System.ComponentModel.DataAnnotations;

namespace RepairShop.Application.Contracts;

public sealed record MessageTemplateResponse(
    Guid Id,
    Guid ShopId,
    string Key,
    string Title,
    string Body,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed record CreateMessageTemplateRequest(
    [Required, MinLength(3)] string Key,
    [Required, MinLength(2)] string Title,
    [Required, MinLength(2)] string Body,
    bool IsActive = true
);

public sealed record UpdateMessageTemplateRequest(
    [Required, MinLength(2)] string Title,
    [Required, MinLength(2)] string Body,
    bool IsActive = true
);
