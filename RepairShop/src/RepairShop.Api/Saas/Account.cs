using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RepairShop.Api.Security;
using RepairShop.Application.Contracts;
using RepairShop.Application.Security;
using RepairShop.Domain.Common;
using RepairShop.Domain.Premium;
using RepairShop.Domain.Saas;
using RepairShop.Domain.Shops;
using RepairShop.Domain.Users;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public sealed record SignupRequest(string ShopName, string OwnerName, string Email, string Password, string? Phone, string? City);
public sealed record ProfileRequest(int Version, string DisplayName, string? LegalName, string? TaxId, string? TaxCondition, string? Email, string? Phone,
    string? Address, string? City, string? Website, string? LogoDataUrl, string? PrimaryColor, string? ReceiptFooter, bool RequireSignature,
    bool OnlineBookingEnabled, OpeningHours? OpeningHours, bool NotifyEmail, bool NotifySms, bool SurveysEnabled, int PickupReminderDays, string? WeeklySummaryEmail);
public sealed record OpeningHours(int[] Days, string From, string To, int SlotMinutes);
public sealed record OnboardingRequest(int Step, bool Completed);
public sealed record CreateUserRequest(string DisplayName, string Email, string Role, string Password);
public sealed record ResetPasswordRequest(string Password);

public static class ShopProvisioning
{
    public static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length > 40) slug = slug[..40].Trim('-');
        return slug.Length < 3 ? $"taller-{Saas.ShortCode(5).ToLowerInvariant()}" : slug;
    }

    public static async Task<string> UniqueSlug(RepairShopDbContext db, string name)
    {
        var baseSlug = Slugify(name); var slug = baseSlug;
        for (var i = 2; await db.ShopProfiles.AnyAsync(x => x.Slug == slug); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }

    // Profiles for shops created before the SaaS layer existed are created on demand.
    public static async Task<ShopProfile> EnsureProfile(RepairShopDbContext db, Guid shopId)
    {
        var profile = await db.ShopProfiles.SingleOrDefaultAsync(x => x.ShopId == shopId);
        if (profile is not null) return profile;
        var shop = await db.Shops.SingleAsync(x => x.Id == shopId);
        profile = new ShopProfile { ShopId = shopId, DisplayName = shop.Name, Slug = await UniqueSlug(db, shop.Name), Phone = shop.Phone ?? "", Address = shop.AddressLine ?? "", City = shop.City ?? "" };
        db.ShopProfiles.Add(profile);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.Entry(profile).State = EntityState.Detached;
            profile = await db.ShopProfiles.SingleAsync(x => x.ShopId == shopId);
        }
        return profile;
    }

    public static OpeningHours Hours(ShopProfile p) =>
        JsonSerializer.Deserialize<OpeningHours>(p.OpeningHoursJson, Saas.Json) ?? new OpeningHours([1, 2, 3, 4, 5], "09:00", "18:00", 30);
}

[ApiController, Route("api/saas")]
public sealed class AccountController(RepairShopDbContext db, IPasswordHasher hasher, IJwtTokenService jwt, SubscriptionService subscriptions,
    LoginSecurityService loginSecurity, IOptions<SaasOptions> saas) : SaasController(db)
{
    [HttpPost("signup"), AllowAnonymous]
    public async Task<IActionResult> Signup(SignupRequest body)
    {
        if (!saas.Value.SignupEnabled) throw new ForbiddenException("El alta de talleres está deshabilitada.");
        loginSecurity.EnforceRateLimit(HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        var email = Saas.Email(body.Email, required: true);
        if ((body.Password ?? "").Length is < 10 or > 128) throw new DomainException("La contraseña debe tener al menos 10 caracteres.");
        if (await Db.Users.AnyAsync(x => x.Email == email)) throw new DomainException("Ya existe una cuenta con ese email. Iniciá sesión.");
        var now = DateTime.UtcNow;
        var name = Saas.Text(body.ShopName, "Nombre del taller", 2, 120);
        await using var tx = await Db.Database.BeginTransactionAsync();
        var shop = new Shop(name, body.Phone, null, body.City, "AR", now);
        Db.Shops.Add(shop);
        var user = new AppUser(shop.Id, email, Saas.Text(body.OwnerName, "Tu nombre", 2, 120), UserRole.Admin, hasher.Hash(body.Password!), now);
        Db.Users.Add(user);
        Db.PremiumBranches.Add(new PremiumBranch { ShopId = shop.Id, Name = "Casa central", Address = "" });
        Db.ShopProfiles.Add(new ShopProfile { ShopId = shop.Id, DisplayName = name, Slug = await ShopProvisioning.UniqueSlug(Db, name), Email = email, Phone = body.Phone?.Trim() ?? "", City = body.City?.Trim() ?? "" });
        Db.ShopSubscriptions.Add(new ShopSubscription { ShopId = shop.Id, Plan = "Pro", Status = "Trialing", TrialEndsAtUtc = now.AddDays(saas.Value.TrialDays), Price = Plans.Get("Pro", saas.Value).Price });
        Db.BillingEvents.Add(new BillingEvent { ShopId = shop.Id, Kind = "TrialStarted", Detail = $"Prueba gratis de {saas.Value.TrialDays} días con todos los módulos." });
        await Db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { data = new LoginResponse(jwt.CreateToken(user), new UserResponse(user.Id, user.ShopId, user.Email, user.DisplayName, user.Role.ToString())) });
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        var profile = await ShopProvisioning.EnsureProfile(Db, Shop);
        var state = await subscriptions.GetAsync(Shop);
        var user = await Db.Users.Where(x => x.Id == Actor).Select(x => new { x.Id, x.DisplayName, x.Email, Role = x.Role.ToString() }).SingleAsync();
        var unread = await Own<ShopAlert>().CountAsync(x => x.ReadAtUtc == null && (x.UserId == null || x.UserId == Actor));
        return Ok(new { data = new { user, profile = PublicProfile(profile), subscription = state, unreadAlerts = unread, allModules = Plans.Modules } });
    }

    private static object PublicProfile(ShopProfile p) => new
    {
        p.Version, p.Slug, p.DisplayName, p.LegalName, p.TaxId, p.TaxCondition, p.Email, p.Phone, p.Address, p.City, p.Website, p.LogoDataUrl,
        p.PrimaryColor, p.ReceiptFooter, p.RequireSignature, p.OnlineBookingEnabled, OpeningHours = ShopProvisioning.Hours(p), p.NotifyEmail, p.NotifySms,
        p.SurveysEnabled, p.PickupReminderDays, p.WeeklySummaryEmail, p.OnboardingStep, p.OnboardingCompleted,
    };

    [HttpGet("profile"), Authorize]
    public async Task<IActionResult> Profile() => Ok(new { data = PublicProfile(await ShopProvisioning.EnsureProfile(Db, Shop)) });

    [HttpPut("profile"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> SaveProfile(ProfileRequest b)
    {
        var p = await ShopProvisioning.EnsureProfile(Db, Shop);
        CheckVersion(p, b.Version);
        p.DisplayName = Saas.Text(b.DisplayName, "Nombre comercial", 2, 120);
        p.LegalName = Saas.Text(b.LegalName, "Razón social", 0, 160);
        var taxId = new string((b.TaxId ?? "").Where(char.IsDigit).ToArray());
        if (taxId.Length is not (0 or 11)) throw new DomainException("El CUIT debe tener 11 dígitos.");
        p.TaxId = taxId;
        p.TaxCondition = b.TaxCondition is "ResponsableInscripto" or "Monotributo" or "Exento" ? b.TaxCondition : throw new DomainException("Condición frente al IVA inválida.");
        p.Email = Saas.Email(b.Email);
        p.Phone = Saas.Text(b.Phone, "Teléfono", 0, 40);
        p.Address = Saas.Text(b.Address, "Dirección", 0, 200);
        p.City = Saas.Text(b.City, "Ciudad", 0, 80);
        p.Website = Saas.Text(b.Website, "Sitio web", 0, 200);
        var logo = b.LogoDataUrl ?? "";
        if (logo.Length > 0 && (!(logo.StartsWith("data:image/png;base64,") || logo.StartsWith("data:image/jpeg;base64,") || logo.StartsWith("data:image/svg+xml;base64,")) || logo.Length > 380_000))
            throw new DomainException("El logo debe ser PNG, JPG o SVG de hasta 280 KB.");
        p.LogoDataUrl = logo;
        p.PrimaryColor = System.Text.RegularExpressions.Regex.IsMatch(b.PrimaryColor ?? "", "^#[0-9a-fA-F]{6}$") ? b.PrimaryColor! : "#2563eb";
        p.ReceiptFooter = Saas.Text(b.ReceiptFooter, "Pie del comprobante", 0, 600);
        p.RequireSignature = b.RequireSignature;
        p.OnlineBookingEnabled = b.OnlineBookingEnabled;
        if (b.OpeningHours is { } h)
        {
            if (h.Days.Any(d => d is < 0 or > 6) || !TimeOnly.TryParse(h.From, out var from) || !TimeOnly.TryParse(h.To, out var to) || from >= to || h.SlotMinutes is < 10 or > 240)
                throw new DomainException("Revisá el horario de atención.");
            p.OpeningHoursJson = JsonSerializer.Serialize(h with { Days = h.Days.Distinct().Order().ToArray() }, Saas.Json);
        }
        p.NotifyEmail = b.NotifyEmail; p.NotifySms = b.NotifySms; p.SurveysEnabled = b.SurveysEnabled;
        p.PickupReminderDays = b.PickupReminderDays is >= 0 and <= 60 ? b.PickupReminderDays : throw new DomainException("Los días para recordar el retiro van de 0 a 60.");
        p.WeeklySummaryEmail = Saas.Email(b.WeeklySummaryEmail);
        var shop = await Db.Shops.SingleAsync(x => x.Id == Shop);
        shop.Update(p.DisplayName, p.Phone, p.Address, p.City, "AR", DateTime.UtcNow);
        await Db.SaveChangesAsync();
        return Ok(new { data = PublicProfile(p) });
    }

    [HttpPost("onboarding"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Onboarding(OnboardingRequest b)
    {
        var p = await ShopProvisioning.EnsureProfile(Db, Shop);
        p.OnboardingStep = Math.Clamp(b.Step, 0, 10); p.OnboardingCompleted = b.Completed; p.Version++;
        await Db.SaveChangesAsync();
        return Ok(new { data = new { p.OnboardingStep, p.OnboardingCompleted } });
    }

    [HttpGet("users"), Authorize]
    public async Task<IActionResult> Users() =>
        Ok(new { data = await Db.Users.Where(x => x.ShopId == Shop).OrderBy(x => x.DisplayName).Select(x => new { x.Id, x.DisplayName, x.Email, Role = x.Role.ToString(), x.CreatedAtUtc }).ToListAsync() });

    [HttpPost("users"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> CreateUser(CreateUserRequest b)
    {
        var email = Saas.Email(b.Email, required: true);
        if (await Db.Users.AnyAsync(x => x.Email == email)) throw new DomainException("Ese email ya tiene una cuenta.");
        if ((b.Password ?? "").Length is < 10 or > 128) throw new DomainException("La contraseña inicial debe tener al menos 10 caracteres.");
        var role = b.Role == "Admin" ? UserRole.Admin : b.Role == "Tech" ? UserRole.Tech : throw new DomainException("Rol inválido.");
        var user = new AppUser(Shop, email, Saas.Text(b.DisplayName, "Nombre", 2, 120), role, hasher.Hash(b.Password!), DateTime.UtcNow);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return Ok(new { data = new { user.Id } });
    }

    [HttpPost("users/{id:guid}/password"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest b)
    {
        var user = await Db.Users.SingleOrDefaultAsync(x => x.Id == id && x.ShopId == Shop) ?? throw new RepairShop.Application.Common.NotFoundException("Usuario no encontrado.");
        if ((b.Password ?? "").Length is < 10 or > 128) throw new DomainException("La contraseña debe tener al menos 10 caracteres.");
        user.ChangePassword(hasher.Hash(b.Password!), DateTime.UtcNow);
        await Db.SaveChangesAsync();
        return Ok(new { data = new { ok = true } });
    }
}
