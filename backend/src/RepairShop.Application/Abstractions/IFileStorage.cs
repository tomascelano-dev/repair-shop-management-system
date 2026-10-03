namespace RepairShop.Application.Abstractions;

/// <summary>Binary storage for uploaded files (local disk, S3, Cloudflare R2, MinIO...).</summary>
public interface IFileStorage
{
    string ProviderName { get; }
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);

    /// <summary>Pre-signed direct URL when the provider supports it (S3/R2); null otherwise.</summary>
    string? GetPresignedUrl(string key, string contentType, TimeSpan ttl);
}

/// <summary>Builds short-lived URLs to download files without a bearer token (img tags, PDFs).</summary>
public interface IFileUrlSigner
{
    string GetUrl(Guid fileId, TimeSpan ttl);
    bool Validate(Guid fileId, long expiresUnix, string signature);
}
