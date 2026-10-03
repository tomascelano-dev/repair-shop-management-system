using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RepairShop.Application.Abstractions;

namespace RepairShop.Infrastructure.Files;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment env)
    {
        var configured = options.Value.Local.RootPath;
        _root = Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured));
        Directory.CreateDirectory(_root);
    }

    public string ProviderName => "local";

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(fs, ct);
        }
        File.Move(tmp, path, overwrite: true);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        Stream? stream = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public string? GetPresignedUrl(string key, string contentType, TimeSpan ttl) => null;

    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('\\', '/').TrimStart('/')));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage key.");
        return full;
    }
}
