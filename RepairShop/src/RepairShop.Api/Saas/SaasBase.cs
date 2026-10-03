using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairShop.Api.Common;
using RepairShop.Api.V2;
using RepairShop.Application.Common;
using RepairShop.Domain.Common;
using RepairShop.Domain.Premium;
using RepairShop.Domain.RepairOrders;
using RepairShop.Infrastructure.Persistence;

namespace RepairShop.Api.Saas;

public static class Saas
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Text(string? value, string label, int min = 1, int max = 200)
    {
        value = (value ?? "").Trim();
        if (value.Length < min || value.Length > max) throw new DomainException($"{label}: ingresá entre {min} y {max} caracteres.");
        return value;
    }

    public static decimal Money(decimal value, string label = "El importe")
    {
        if (value < 0 || value > 1_000_000_000m) throw new DomainException($"{label} debe estar entre 0 y 1.000.000.000.");
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static string Currency(string? currency)
    {
        currency = (currency ?? "ARS").Trim().ToUpperInvariant();
        if (currency is not ("ARS" or "USD")) throw new DomainException("Elegí ARS o USD.");
        return currency;
    }

    public static string Method(string? method)
    {
        method = (method ?? "Cash").Trim();
        if (method is not ("Cash" or "Card" or "Transfer" or "MercadoPago" or "Account")) throw new DomainException("Medio de pago inválido.");
        return method;
    }

    public static string Email(string? value, bool required = false)
    {
        value = (value ?? "").Trim().ToLowerInvariant();
        if (value.Length == 0 && !required) return "";
        if (value.Length > 160 || !System.Net.Mail.MailAddress.TryCreate(value, out _)) throw new DomainException("Ingresá un email válido.");
        return value;
    }

    public static readonly System.Globalization.NumberFormatInfo ArgentineNumbers = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };
    private static readonly string[] Days = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    // The API runs in invariant-globalization mode, so Spanish dates are formatted by hand.
    public static string When(DateTime local, bool withWords = true) =>
        $"{Days[(int)local.DayOfWeek]} {local.Day}/{local.Month}{(withWords ? " a las" : "")} {local:HH:mm}";

    public static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string Token(int bytes = 32) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes));

    // Readable code without ambiguous characters, for tracking links and printed tickets.
    public static string ShortCode(int length = 8)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Create(length, 0, (span, _) => { for (var i = 0; i < span.Length; i++) span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]; });
    }

    public static string PaymentMethodName(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        PaymentMethod.Transfer => "Transfer",
        PaymentMethod.MercadoPago => "MercadoPago",
        _ => "Transfer",
    };
}

// Shared plumbing for SaaS controllers: tenant scoping, idempotent transactional commands and optimistic versions.
public abstract class SaasController(RepairShopDbContext db) : ControllerBase
{
    protected RepairShopDbContext Db => db;
    protected Guid Shop => CurrentUser.GetShopId(User) is var id && id != Guid.Empty ? id : throw new UnauthorizedAccessException();
    protected Guid Actor => CurrentUser.GetUserId(User);
    protected bool IsAdmin => User.IsInRole("Admin");

    protected void RequireAdmin()
    {
        if (!IsAdmin) throw new ForbiddenException("Solo un administrador puede hacer esto.");
    }

    protected IQueryable<T> Own<T>() where T : class, IPremiumRecord => db.Set<T>().Where(x => x.ShopId == Shop);

    protected async Task<T> Find<T>(Guid id) where T : class, IPremiumRecord =>
        await Own<T>().SingleOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("Registro no encontrado en este taller.");

    protected static void CheckVersion(IPremiumRecord entity, int version)
    {
        if (entity.Version != version) throw new WorkflowConflict("El registro cambió. Actualizá la pantalla y volvé a intentar.");
        entity.Version++;
    }

    // Same contract as the premium commands: Idempotency-Key header, request fingerprint, one tenant lock per transaction.
    protected async Task<IActionResult> Change(string operation, object body, Func<Task<object>> action)
    {
        var shop = Shop;
        var key = Request.Headers["Idempotency-Key"].ToString();
        if (key.Length is < 8 or > 100) throw new DomainException("Falta una clave válida de idempotencia.");
        var hash = Saas.Sha256(JsonSerializer.Serialize(body, Saas.Json));
        await using var tx = await db.Database.BeginTransactionAsync();
        var lockName = $"premium:{shop}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockName}, 0))");
        var receipt = await db.WorkflowRequests.SingleOrDefaultAsync(x => x.ShopId == shop && x.Operation == operation && x.Key == key);
        if (receipt is not null)
        {
            if (receipt.Hash != hash) throw new WorkflowConflict("Esta clave ya se usó con otros datos.");
            return Ok(new { data = JsonSerializer.Deserialize<JsonElement>(receipt.ResponseJson) });
        }
        var result = await action();
        db.WorkflowRequests.Add(new WorkflowRequest { ShopId = shop, Operation = operation, Key = key, Hash = hash, ResponseJson = JsonSerializer.Serialize(result, Saas.Json) });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { data = result });
    }

    protected async Task<(WorkshopWorkflow W, RepairOrder O)> LockOrder(Guid id)
    {
        var shop = Shop;
        var w = await db.Workflows.FromSqlInterpolated($"SELECT * FROM workflows WHERE \"Id\"={id} AND \"ShopId\"={shop} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new NotFoundException("Orden no encontrada en este taller.");
        var o = await db.RepairOrders.SingleAsync(x => x.Id == id && x.ShopId == shop);
        return (w, o);
    }

    protected void Note(Guid order, string message) => db.RepairOrderNotes.Add(new RepairOrderNote(Shop, order, message, Actor, DateTime.UtcNow));
}
