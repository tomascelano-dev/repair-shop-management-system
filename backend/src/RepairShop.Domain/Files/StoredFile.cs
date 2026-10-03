using RepairShop.Domain.Common;

namespace RepairShop.Domain.Files;

/// <summary>
/// Metadata of an uploaded file (photo, signature, logo, document). Bytes live in the storage provider (disk / S3 / R2).
/// </summary>
public sealed class StoredFile : IShopScoped
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ShopId { get; private set; }

    public string StorageKey { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = null!;

    // "order_photo", "signature", "logo", "document"...
    public string Purpose { get; private set; } = null!;

    public Guid? CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private StoredFile() { }

    public StoredFile(Guid shopId, Guid id, string storageKey, string fileName, string contentType, long sizeBytes, string sha256, string purpose, Guid? userId, DateTime nowUtc)
    {
        if (id == Guid.Empty) throw new DomainException("Id de archivo inválido.");
        if (string.IsNullOrWhiteSpace(storageKey)) throw new DomainException("Falta la ubicación del archivo.");
        if (sizeBytes <= 0) throw new DomainException("El archivo está vacío.");

        Id = id;
        ShopId = shopId;
        StorageKey = storageKey;
        FileName = SanitizeFileName(fileName);
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        Purpose = string.IsNullOrWhiteSpace(purpose) ? "document" : purpose.Trim();
        CreatedByUserId = userId == Guid.Empty ? null : userId;
        CreatedAtUtc = nowUtc;
    }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "").Trim();
        var cleaned = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ' ? c : '_').ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "archivo";
        return cleaned.Length > 150 ? cleaned[^150..] : cleaned;
    }
}
