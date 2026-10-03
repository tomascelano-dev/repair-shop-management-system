using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;

namespace RepairShop.Infrastructure.Files;

/// <summary>S3-compatible storage (AWS S3, Cloudflare R2, MinIO, Backblaze B2...).</summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly StorageOptions.S3Options _opt;
    private readonly AmazonS3Client _client;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        _opt = options.Value.S3;
        if (string.IsNullOrWhiteSpace(_opt.Bucket)) throw new InvalidOperationException("Storage:S3:Bucket is required when Storage:Provider=S3.");

        var config = new AmazonS3Config { ForcePathStyle = _opt.ForcePathStyle };
        if (!string.IsNullOrWhiteSpace(_opt.ServiceUrl))
        {
            config.ServiceURL = _opt.ServiceUrl;
            config.AuthenticationRegion = string.IsNullOrWhiteSpace(_opt.Region) ? "auto" : _opt.Region;
        }
        else if (!string.IsNullOrWhiteSpace(_opt.Region) && _opt.Region != "auto")
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_opt.Region);
        }

        _client = string.IsNullOrWhiteSpace(_opt.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(_opt.AccessKey, _opt.SecretKey), config);
    }

    public string ProviderName => "s3";

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opt.Bucket,
            Key = FullKey(key),
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var res = await _client.GetObjectAsync(_opt.Bucket, FullKey(key), ct);
            return res.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct)
        => _client.DeleteObjectAsync(_opt.Bucket, FullKey(key), ct);

    public string? GetPresignedUrl(string key, string contentType, TimeSpan ttl)
        => _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _opt.Bucket,
            Key = FullKey(key),
            Expires = DateTime.UtcNow.Add(ttl),
            Verb = HttpVerb.GET,
            ResponseHeaderOverrides = { ContentType = contentType }
        });

    private string FullKey(string key)
        => string.IsNullOrWhiteSpace(_opt.KeyPrefix) ? key : $"{_opt.KeyPrefix.TrimEnd('/')}/{key}";

    public void Dispose() => _client.Dispose();
}
