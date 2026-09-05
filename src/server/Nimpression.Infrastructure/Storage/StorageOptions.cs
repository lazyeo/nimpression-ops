namespace Nimpression.Infrastructure.Storage;

/// <summary>
/// S3 / MinIO 对象存储配置选项。
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>服务 Endpoint，如 http://localhost:9000</summary>
    public string Endpoint { get; set; } = "http://localhost:9000";

    /// <summary>MinIO / S3 AccessKey</summary>
    public string AccessKey { get; set; } = "nimpression";

    /// <summary>MinIO / S3 SecretKey</summary>
    public string SecretKey { get; set; } = "dev-only-insecure-minio-secret-key";

    /// <summary>多媒体文件桶名（默认私有）</summary>
    public string MediaBucketName { get; set; } = "nimpression-media";

    /// <summary>数据导出桶名（默认私有）</summary>
    public string ExportsBucketName { get; set; } = "nimpression-exports";

    /// <summary>是否使用 SSL</summary>
    public bool UseSsl { get; set; }

    /// <summary>是否强制使用 Path 路径风格（MinIO 必须为 true）</summary>
    public bool ForcePathStyle { get; set; } = true;
}
