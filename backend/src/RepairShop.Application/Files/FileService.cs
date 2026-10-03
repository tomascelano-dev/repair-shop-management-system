using System.Security.Cryptography;
using RepairShop.Application.Abstractions;
using RepairShop.Application.Common;
using RepairShop.Domain.Common;
using RepairShop.Domain.Files;

namespace RepairShop.Application.Files;

public sealed record FileDownload(Stream Content, string ContentType, string FileName);

/// <summary>
/// Validates and stores uploads. The content type is detected from the file signature (magic bytes),
/// never trusted from the client.
/// </summary>
public sealed class FileService
{
    public const long MaxImageBytes = 10 * 1024 * 1024;
    public const long MaxDocumentBytes = 15 * 1024 * 1024;
    public const long MaxSignatureBytes = 600 * 1024;

    private readonly IFileStorage _storage;
    private readonly IStoredFileRepository _files;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public FileService(IFileStorage storage, IStoredFileRepository files, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _storage = storage;
        _files = files;
        _uow = uow;
        _clock = clock;
    }

    public async Task<StoredFile> UploadAsync(Guid shopId, Stream content, string? fileName, string purpose, bool imagesOnly, Guid? userId, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        return await StoreAsync(shopId, bytes, fileName, purpose, imagesOnly, userId, ct);
    }

    public async Task<StoredFile> SaveDataUrlAsync(Guid shopId, string dataUrl, string fileName, string purpose, Guid? userId, CancellationToken ct)
    {
        // data:image/png;base64,....
        var comma = (dataUrl ?? "").IndexOf(',');
        if (comma < 0 || !dataUrl!.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || !dataUrl[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("La firma debe ser una imagen (data URL base64).");

        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]); }
        catch (FormatException) { throw new DomainException("La imagen de la firma está dañada."); }

        if (bytes.Length > MaxSignatureBytes) throw new DomainException("La imagen de la firma es demasiado grande.");
        return await StoreAsync(shopId, bytes, fileName, purpose, imagesOnly: true, userId, ct);
    }

    public async Task<FileDownload> OpenAsync(Guid shopId, Guid fileId, CancellationToken ct)
    {
        var file = await _files.GetByIdAsync(shopId, fileId, ct) ?? throw new NotFoundException("Archivo no encontrado.");
        return await OpenAsync(file, ct);
    }

    public async Task<FileDownload> OpenAnyShopAsync(Guid fileId, CancellationToken ct)
    {
        var file = await _files.GetByIdAnyShopAsync(fileId, ct) ?? throw new NotFoundException("Archivo no encontrado.");
        return await OpenAsync(file, ct);
    }

    public async Task<byte[]?> ReadBytesAsync(Guid shopId, Guid? fileId, CancellationToken ct)
    {
        if (fileId is null) return null;
        var file = await _files.GetByIdAsync(shopId, fileId.Value, ct);
        if (file is null) return null;
        await using var stream = await _storage.OpenReadAsync(file.StorageKey, ct);
        if (stream is null) return null;
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private async Task<FileDownload> OpenAsync(StoredFile file, CancellationToken ct)
    {
        var stream = await _storage.OpenReadAsync(file.StorageKey, ct) ?? throw new NotFoundException("El archivo ya no está disponible.");
        return new FileDownload(stream, file.ContentType, file.FileName);
    }

    private async Task<StoredFile> StoreAsync(Guid shopId, byte[] bytes, string? fileName, string purpose, bool imagesOnly, Guid? userId, CancellationToken ct)
    {
        if (bytes.Length == 0) throw new DomainException("El archivo está vacío.");

        var (contentType, ext) = Sniff(bytes);
        if (contentType is null) throw new DomainException("Formato no soportado. Subí una imagen (JPG, PNG, WEBP, HEIC) o un PDF.");
        var isImage = contentType.StartsWith("image/", StringComparison.Ordinal);
        if (imagesOnly && !isImage) throw new DomainException("Solo se permiten imágenes.");
        if (bytes.Length > (isImage ? MaxImageBytes : MaxDocumentBytes))
            throw new DomainException($"El archivo supera el máximo permitido ({(isImage ? MaxImageBytes : MaxDocumentBytes) / 1024 / 1024} MB).");

        var now = _clock.UtcNow;
        var id = Guid.NewGuid();
        var key = $"{shopId:N}/{now:yyyy}/{now:MM}/{id:N}{ext}";
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        await using (var ms = new MemoryStream(bytes, writable: false))
        {
            await _storage.SaveAsync(key, ms, contentType, ct);
        }

        var name = string.IsNullOrWhiteSpace(fileName) ? $"archivo{ext}" : fileName;
        if (!Path.HasExtension(name)) name += ext;

        var file = new StoredFile(shopId, id, key, name, contentType, bytes.Length, sha, purpose, userId, now);
        await _files.AddAsync(file, ct);
        await _uow.SaveChangesAsync(ct);
        return file;
    }

    /// <summary>Detects the real file type from its first bytes.</summary>
    public static (string? ContentType, string Extension) Sniff(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ("image/png", ".png");
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ("image/webp", ".webp");
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8') return ("image/gif", ".gif");
        if (b.Length >= 12 && b[4] == 'f' && b[5] == 't' && b[6] == 'y' && b[7] == 'p')
        {
            var brand = System.Text.Encoding.ASCII.GetString(b.Slice(8, 4));
            if (brand is "heic" or "heix" or "hevc" or "mif1" or "msf1") return ("image/heic", ".heic");
        }
        if (b.Length >= 5 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F' && b[4] == '-') return ("application/pdf", ".pdf");
        return (null, "");
    }
}
