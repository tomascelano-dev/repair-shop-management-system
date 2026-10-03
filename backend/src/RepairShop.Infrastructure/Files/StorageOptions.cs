namespace RepairShop.Infrastructure.Files;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>"Local" (default) or "S3" (AWS S3, Cloudflare R2, MinIO...).</summary>
    public string Provider { get; set; } = "Local";

    public LocalOptions Local { get; set; } = new();
    public S3Options S3 { get; set; } = new();

    public sealed class LocalOptions
    {
        /// <summary>Absolute path or relative to the content root. Mount a volume here in Docker.</summary>
        public string RootPath { get; set; } = "data/uploads";
    }

    public sealed class S3Options
    {
        public string Bucket { get; set; } = "";
        /// <summary>Custom endpoint for R2/MinIO (e.g. https://&lt;account&gt;.r2.cloudflarestorage.com). Empty for AWS.</summary>
        public string ServiceUrl { get; set; } = "";
        public string Region { get; set; } = "auto";
        public string AccessKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
        public bool ForcePathStyle { get; set; } = true;
        public string KeyPrefix { get; set; } = "";
    }
}
