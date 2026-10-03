using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RepairShop.Api.Security;
using RepairShop.Application.Abstractions;

namespace RepairShop.Api.Common;

/// <summary>
/// Short-lived HMAC-signed URLs for files (photos, logos, signatures) so they can be used in &lt;img&gt;
/// tags and PDFs without exposing the bearer token. Key derived from the JWT key unless Files:SigningKey is set.
/// </summary>
public sealed class FileUrlSigner : IFileUrlSigner
{
    private readonly byte[] _key;
    private readonly string _apiBase;

    public FileUrlSigner(IConfiguration config, IOptions<JwtOptions> jwt, IOptions<AppOptions> app)
    {
        var secret = config["Files:SigningKey"];
        _key = string.IsNullOrWhiteSpace(secret)
            ? SHA256.HashData(Encoding.UTF8.GetBytes("files|" + jwt.Value.Key))
            : Encoding.UTF8.GetBytes(secret);
        _apiBase = app.Value.PublicApiUrl.TrimEnd('/');
    }

    public string GetUrl(Guid fileId, TimeSpan ttl)
    {
        var expires = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
        return $"{_apiBase}/api/v1/files/{fileId}?exp={expires}&sig={Sign(fileId, expires)}";
    }

    public bool Validate(Guid fileId, long expiresUnix, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || expiresUnix < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
        var expected = Encoding.ASCII.GetBytes(Sign(fileId, expiresUnix));
        var actual = Encoding.ASCII.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private string Sign(Guid fileId, long expires)
    {
        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{fileId:N}|{expires}"));
        return Convert.ToBase64String(mac, 0, 18).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
