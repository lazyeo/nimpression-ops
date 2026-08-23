namespace Nimpression.Application.Features.Drivers.Storage;

/// <summary>
/// 对象存储服务契约。用于头像、罚单照片、里程照片与导出文件的存储与短时效预签名 URL 获取。
/// </summary>
public interface IObjectStorageService
{
    /// <summary>
    /// 上传对象并返回存储 key。
    /// </summary>
    Task<string> UploadAsync(
        string bucketName,
        string key,
        Stream data,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载对象流。如果不存在返回 null。
    /// </summary>
    Task<Stream?> DownloadAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取对象的短时效预签名下载 URL（≤15分钟）。
    /// </summary>
    Task<string> GetPresignedUrlAsync(
        string bucketName,
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定对象。
    /// </summary>
    Task DeleteAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查指定对象是否存在。
    /// </summary>
    Task<bool> ExistsAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default);
}
