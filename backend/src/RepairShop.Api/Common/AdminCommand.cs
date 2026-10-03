using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Security;
using RepairShop.Domain.Users;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Common;

/// <summary>
/// Server-side account bootstrap and recovery, run from the API container:
///   dotnet RepairShop.Api.dll admin create &lt;email&gt; ["Nombre"] ["Nombre del taller"]
/// A new email becomes an administrator (the first run also creates the shop); an existing email gets a fresh
/// link to set its password. Prints a one-time link: the password is chosen in the browser, never on the server.
/// </summary>
internal static class AdminCommand
{
    private const string Usage = "Uso: admin create <email> [\"Nombre\"] [\"Nombre del taller\"]";

    public static bool Matches(string[] args)
        => args.Length >= 2
           && string.Equals(args[0], "admin", StringComparison.OrdinalIgnoreCase)
           && string.Equals(args[1], "create", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var email = AppUser.NormalizeEmail(args.ElementAtOrDefault(2));
        if (email.Length is < 5 or > 200 || !email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var name = args.ElementAtOrDefault(3)?.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = email.Split('@')[0];
        var shopName = args.ElementAtOrDefault(4)?.Trim();
        if (string.IsNullOrWhiteSpace(shopName)) shopName = "Mi taller";

        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<RepairShopDbContext>();
        var clock = sp.GetRequiredService<IDateTimeProvider>();
        var links = sp.GetRequiredService<IAppLinks>();
        var now = clock.UtcNow;

        var raw = TokenHasher.NewToken();
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Email == email);
        string url;
        DateTime expires;

        if (user is null)
        {
            var shop = await DbSeeder.EnsureShopAsync(db, clock, shopName);
            var hasher = sp.GetRequiredService<IPasswordHasher>();
            // Unusable random password until the invitation is accepted.
            user = new AppUser(shop.Id, email, name, UserRole.Admin, hasher.Hash(TokenHasher.NewToken()), now);
            expires = now.AddDays(7);
            user.SetPendingToken(UserTokenPurpose.Invitation, TokenHasher.Hash(raw), expires, now);
            await db.Users.AddAsync(user);
            url = links.Invitation(raw);
            Console.WriteLine($"Administrador creado: {email} en \"{shop.Name}\".");
        }
        else
        {
            if (!user.IsActive) user.SetActive(true, now);
            var invitation = user.HasPendingInvitation;
            expires = now.AddDays(invitation ? 7 : 1);
            user.SetPendingToken(invitation ? UserTokenPurpose.Invitation : UserTokenPurpose.PasswordReset, TokenHasher.Hash(raw), expires, now);
            url = invitation ? links.Invitation(raw) : links.PasswordReset(raw);
            Console.WriteLine($"El usuario {email} ya existía (rol {user.Role}): se generó un link nuevo para elegir la contraseña.");
        }

        await db.SaveChangesAsync();
        Console.WriteLine();
        Console.WriteLine($"Abrí este link para elegir la contraseña (vence {expires:dd/MM/yyyy HH:mm} UTC, se usa una sola vez):");
        Console.WriteLine(url);
        return 0;
    }
}
