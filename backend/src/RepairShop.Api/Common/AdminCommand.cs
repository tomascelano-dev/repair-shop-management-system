using Microsoft.EntityFrameworkCore;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Security;
using RepairShop.Domain.Billing;
using RepairShop.Domain.Users;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Common;

/// <summary>
/// Server-side account bootstrap and recovery, run from the API container:
///   dotnet RepairShop.Api.dll admin create &lt;email&gt; ["Nombre"] ["Nombre del taller"]
///   dotnet RepairShop.Api.dll admin plan &lt;email&gt; &lt;Basic|Standard|Pro&gt;
///   dotnet RepairShop.Api.dll admin signups [días]
/// A new email becomes an administrator (the first run also creates the shop); an existing email gets a fresh
/// link to set its password. Prints a one-time link: the password is chosen in the browser, never on the server.
/// </summary>
internal static class AdminCommand
{
    private const string Usage = "Uso: admin create <email> [\"Nombre\"] [\"Nombre del taller\"]\n     admin plan <email> <Basic|Standard|Pro>\n     admin signups [días]";

    public static bool Matches(string[] args)
        => args.Length >= 2
           && string.Equals(args[0], "admin", StringComparison.OrdinalIgnoreCase)
           && args[1].ToLowerInvariant() is "create" or "plan" or "signups";

    public static Task<int> RunAsync(WebApplication app, string[] args) => args[1].ToLowerInvariant() switch
    {
        "plan" => GrantPlanAsync(app, args),
        "signups" => ListSignupsAsync(app, args),
        _ => CreateAsync(app, args)
    };

    /// <summary>Recent self-service signups with their subscription and the campaign they came from (tab-separated).</summary>
    private static async Task<int> ListSignupsAsync(WebApplication app, string[] args)
    {
        var days = int.TryParse(args.ElementAtOrDefault(2), out var d) && d > 0 ? d : 30;
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairShopDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().UtcNow;
        var since = now.AddDays(-days);

        var signups = await db.SignupAttributions.Where(x => x.CreatedAtUtc >= since).OrderByDescending(x => x.CreatedAtUtc).ToListAsync();
        var orgs = signups.Select(x => x.OrganizationId).ToList();
        var users = signups.Select(x => x.UserId).ToList();
        var shops = await db.Shops.Where(x => orgs.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var emails = await db.Users.IgnoreQueryFilters().Where(x => users.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Email);
        var subs = await db.Subscriptions.Where(x => orgs.Contains(x.OrganizationId)).ToDictionaryAsync(x => x.OrganizationId);

        Console.WriteLine($"Altas de los últimos {days} días: {signups.Count} ({signups.Count(x => x.FromCampaign)} desde campañas)");
        Console.WriteLine(string.Join('\t', "fecha", "taller", "email", "país", "estado", "plan", "origen", "medio", "campaña", "anuncio", "clic"));
        foreach (var x in signups)
        {
            subs.TryGetValue(x.OrganizationId, out var sub);
            var click = (x.Gclid ?? x.Gbraid ?? x.Wbraid) is not null ? "google" : x.Fbclid is not null ? "meta" : "";
            Console.WriteLine(string.Join('\t',
                x.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"),
                shops.GetValueOrDefault(x.OrganizationId, "?"),
                emails.GetValueOrDefault(x.UserId, "?"),
                sub?.BillingCountry ?? "",
                sub?.EffectiveStatus(now).ToString() ?? "",
                sub?.Plan.ToString() ?? "",
                x.Source ?? (x.Referrer is null ? "(directo)" : "(referido)"),
                x.Medium ?? "",
                x.Campaign ?? "",
                x.Content ?? "",
                click));
        }
        return 0;
    }

    /// <summary>Gives the organization of the user a complimentary plan (no charges, never expires).</summary>
    private static async Task<int> GrantPlanAsync(WebApplication app, string[] args)
    {
        var email = AppUser.NormalizeEmail(args.ElementAtOrDefault(2));
        if (email.Length < 5 || !Plans.TryParse(args.ElementAtOrDefault(3), out var planId))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RepairShopDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().UtcNow;

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Email == email);
        if (user is null)
        {
            Console.Error.WriteLine($"No existe un usuario con el email {email}.");
            return 1;
        }

        var shop = await db.Shops.FirstAsync(x => x.Id == user.ShopId);
        var subscription = await db.Subscriptions.FirstOrDefaultAsync(x => x.OrganizationId == shop.OrganizationId);
        if (subscription is null)
        {
            subscription = Subscription.Complimentary(shop.OrganizationId, planId, "AR", now);
            await db.Subscriptions.AddAsync(subscription);
        }
        else
        {
            if (subscription.HasLiveProviderSubscription)
                Console.WriteLine($"Atención: la organización tiene una suscripción paga en {subscription.Provider} ({subscription.ProviderSubscriptionId}). Cancelala allá para que no se le siga cobrando.");
            subscription.GrantComplimentary(planId, now);
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"Plan {Plans.Get(planId).Name} bonificado para \"{shop.Name}\" ({email}).");
        return 0;
    }

    private static async Task<int> CreateAsync(WebApplication app, string[] args)
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
