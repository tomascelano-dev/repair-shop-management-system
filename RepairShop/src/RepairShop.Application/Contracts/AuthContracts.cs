using System.ComponentModel.DataAnnotations;

namespace RepairShop.Application.Contracts;

public sealed record LoginRequest(
    [Required] string Email,
    [Required, MinLength(6)] string Password
);

public sealed record LoginResponse(
    string AccessToken,
    UserResponse User
);

public sealed record UserResponse(
    Guid Id,
    Guid ShopId,
    string Email,
    string DisplayName,
    string Role
);
