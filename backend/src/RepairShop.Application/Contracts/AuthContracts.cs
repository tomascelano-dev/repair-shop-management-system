using System.ComponentModel.DataAnnotations;

namespace RepairShop.Application.Contracts;

public sealed record LoginRequest(
    [Required] string Email,
    [Required, MinLength(6)] string Password
);

public sealed record LoginResponse(
    string AccessToken,
    UserResponse User,
    DateTime AccessTokenExpiresAtUtc,
    IReadOnlyList<ShopAccessResponse> Shops,
    IReadOnlyList<string> Permissions
);

public sealed record UserResponse(
    Guid Id,
    Guid ShopId,
    string Email,
    string DisplayName,
    string Role,
    string? ShopName = null,
    Guid? OrganizationId = null,
    bool EmailVerified = true
);

public sealed record ShopAccessResponse(Guid ShopId, string ShopName, string Role, bool IsHome);

public sealed record SwitchShopRequest([Required] Guid ShopId);

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword
);

public sealed record ForgotPasswordRequest([Required] string Email);

public sealed record ResetPasswordRequest(
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword
);

public sealed record AcceptInvitationRequest(
    [Required] string Token,
    [Required, MinLength(8)] string Password,
    string? DisplayName
);

public sealed record TokenInfoResponse(string Email, string DisplayName, string ShopName, string Purpose, DateTime ExpiresAtUtc);

/// <summary>Result of a successful authentication: response body + the refresh token to set as cookie.</summary>
public sealed record AuthResult(LoginResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);
