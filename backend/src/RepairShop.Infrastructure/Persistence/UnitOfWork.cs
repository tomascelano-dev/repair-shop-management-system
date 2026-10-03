using Microsoft.EntityFrameworkCore;
using Npgsql;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;

namespace RepairShop.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly RepairShopDbContext _db;
    public UnitOfWork(RepairShopDbContext db) => _db = db;

    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            throw pg.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => new ConflictException(DescribeUnique(pg.ConstraintName), ex),
                PostgresErrorCodes.ForeignKeyViolation => new ConflictException("La operación referencia datos inexistentes o que están en uso por otros registros.", ex),
                _ => ex
            };
        }
    }

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        if (_db.Database.CurrentTransaction is not null) return await work(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var result = await work(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<T> RetryOnConflictAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct, int maxAttempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await work(ct);
            }
            catch (ConcurrencyConflictException) when (attempt < maxAttempts && _db.Database.CurrentTransaction is null)
            {
                _db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), ct);
            }
        }
    }

    public void Detach(object entity) => _db.Entry(entity).State = EntityState.Detached;

    private static string DescribeUnique(string? constraint) => constraint switch
    {
        "IX_inventory_items_ShopId_Sku" => "Ya existe un ítem con ese SKU.",
        "IX_inventory_items_ShopId_Barcode" => "Ya existe un ítem con ese código de barras.",
        "IX_users_Email" => "Ya existe un usuario con ese email.",
        "IX_message_templates_ShopId_Key" => "Ya existe una plantilla con esa clave.",
        "IX_cash_sessions_ShopId_open" => "Ya hay una caja abierta en esta sucursal.",
        "IX_notification_outbox_ShopId_CorrelationKey" => "Ese mensaje ya fue generado.",
        "IX_customer_feedback_RepairOrderId" => "La encuesta de esta orden ya fue respondida.",
        "IX_inventory_item_compatibilities_InventoryItemId_BrandKey_ModelKey" => "Esa compatibilidad ya está cargada.",
        "IX_user_shop_access_UserId_ShopId" => "El usuario ya tiene acceso a esa sucursal.",
        "IX_order_payments_ShopId_ExternalPaymentId" => "Ese pago ya fue registrado.",
        _ => "Ya existe un registro con esos datos."
    };
}

/// <summary>Optimistic concurrency failure (row version mismatch). Mapped to HTTP 409.</summary>
public sealed class ConcurrencyConflictException : ConflictException
{
    public ConcurrencyConflictException(Exception inner)
        : base("El registro fue modificado por otra persona mientras lo editabas. Actualizá la pantalla e intentá de nuevo.", inner)
    {
    }
}
