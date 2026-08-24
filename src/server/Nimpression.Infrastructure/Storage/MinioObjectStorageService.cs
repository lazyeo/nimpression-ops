using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nimpression.Application.Features.Drivers.Storage;

namespace Nimpression.Infrastructure.Storage;

/// <summary>
/// MinIO / S3 兼容对象存储适配器实现。
/// </summary>
public sealed partial class MinioObjectStorageService : IObjectStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly StorageOptions _options;
    private readonly ILogger<MinioObjectStorageService> _logger;
    private readonly bool _disposeClient;

    public MinioObjectStorageService(
        IOptions<StorageOptions> options,
        ILogger<MinioObjectStorageService> logger,
        IAmazonS3? s3Client = null)
    {
        _options = options.Value;
        _logger = logger;

        if (s3Client is not null)
        {
            _s3Client = s3Client;
            _disposeClient = false;
        }
        else
        {
            var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
            var config = new AmazonS3Config
            {
                ServiceURL = _options.Endpoint,
                ForcePathStyle = _options.ForcePathStyle,
                UseHttp = !_options.UseSsl,
            };
            _s3Client = new AmazonS3Client(credentials, config);
            _disposeClient = true;
        }
    }

    public async Task<string> UploadAsync(
        string bucketName,
        string key,
        Stream data,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(data);

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = data,
            ContentType = contentType,
            DisablePayloadSigning = true,
        };

        await _s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        LogObjectUploaded(_logger, bucketName, key);

        return key;
    }

    public async Task<Stream?> DownloadAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key,
            };

            var response = await _s3Client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<string> GetPresignedUrlAsync(
        string bucketName,
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // 约束：照片预签名 URL 最长 15 分钟（F8.4 / F2.2）
        var maxExpiry = TimeSpan.FromMinutes(15);
        var effectiveExpiry = expiry > maxExpiry ? maxExpiry : expiry;
        if (effectiveExpiry <= TimeSpan.Zero)
        {
            effectiveExpiry = TimeSpan.FromMinutes(15);
        }

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Expires = DateTime.UtcNow.Add(effectiveExpiry),
            Verb = HttpVerb.GET,
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public async Task DeleteAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var request = new DeleteObjectRequest
        {
            BucketName = bucketName,
            Key = key,
        };

        await _s3Client.DeleteObjectAsync(request, cancellationToken).ConfigureAwait(false);
        LogObjectDeleted(_logger, bucketName, key);
    }

    public async Task<bool> ExistsAsync(
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = key,
            };

            await _s3Client.GetObjectMetadataAsync(request, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _s3Client.Dispose();
        }
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Uploaded object to bucket {Bucket} with key {Key}")]
    private static partial void LogObjectUploaded(ILogger logger, string bucket, string key);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Deleted object from bucket {Bucket} with key {Key}")]
    private static partial void LogObjectDeleted(ILogger logger, string bucket, string key);
}
