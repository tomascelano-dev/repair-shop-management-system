using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Domain.Notifications;

namespace RepairShop.Application.Security;

/// <summary>
/// Security audit 2026-11-23 (A-06, M-04): signup is open and opens a session right away with any address, so a newcomer
/// could reserve somebody else's email and plant users or payment integrations before the real owner shows up, or use
/// the platform's sender for free text. The actions that act in the name of the account (messages to customers,
/// inviting users, connecting Mercado Pago or ARCA) need a confirmed email once the platform can send one. While no
/// email provider is configured nobody could confirm, so nothing is required (the same behaviour as before).
/// </summary>
public static class AccountTrust
{
    public static async Task RequireConfirmedEmailAsync(IUserRepository users, IIntegrationStatus status, Guid userId, CancellationToken ct)
    {
        if (!status.IsChannelConfigured(NotificationChannel.Email)) return;
        var user = await users.GetByIdAsync(userId, ct) ?? throw new UnauthorizedException("Sesión inválida.");
        if (!user.IsEmailVerified)
            throw new ForbiddenException("Confirmá tu email (te mandamos un link al registrarte) para usar esta función. Podés pedir otro desde tu perfil.");
    }
}
