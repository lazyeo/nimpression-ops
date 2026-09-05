using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nimpression.Infrastructure.Storage;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Storage;

public sealed class ObjectStorageServiceTests
{
    private readonly IAmazonS3 _s3Client = Substitute.For<IAmazonS3>();
    private readonly StorageOptions _options = new()
    {
        Endpoint = "http://localhost:9000",
        AccessKey = "test_key",
        SecretKey = "dev-only-insecure-minio-secret-key",
        MediaBucketName = "nimpression-media",
        ExportsBucketName = "nimpression-exports",
    };

    private MinioObjectStorageService CreateSut()
    {
        return new MinioObjectStorageService(
            Options.Create(_options),
            NullLogger<MinioObjectStorageService>.Instance,
            _s3Client);
    }

    [Fact]
    public async Task UploadAsync_uploads_to_s3_and_returns_key()
    {
        using var sut = CreateSut();
        using var stream = new MemoryStream([0xFF, 0xD8, 0xFF]);

        _s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        var result = await sut.UploadAsync("nimpression-media", "avatars/1.jpg", stream, "image/jpeg");

        result.Should().Be("avatars/1.jpg");
        await _s3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == "nimpression-media" &&
                r.Key == "avatars/1.jpg" &&
                r.ContentType == "image/jpeg"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_returns_stream_when_object_exists()
    {
        using var sut = CreateSut();
        var expectedStream = new MemoryStream([1, 2, 3]);
        var response = new GetObjectResponse { ResponseStream = expectedStream };

        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var result = await sut.DownloadAsync("nimpression-media", "avatars/1.jpg");

        result.Should().BeSameAs(expectedStream);
    }

    [Fact]
    public async Task DownloadAsync_returns_null_when_not_found()
    {
        using var sut = CreateSut();
        var s3Exception = new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound };
        _s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(s3Exception);

        var result = await sut.DownloadAsync("nimpression-media", "avatars/missing.jpg");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPresignedUrlAsync_clamps_expiry_to_max_15_minutes()
    {
        using var sut = CreateSut();
        _s3Client.GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>())
            .Returns("http://localhost:9000/signed-url");

        // 请求 60 分钟，预期被 clamp 为 15 分钟（F8.4 / F2.2）
        var url = await sut.GetPresignedUrlAsync("nimpression-media", "avatars/1.jpg", TimeSpan.FromMinutes(60));

        url.Should().Be("http://localhost:9000/signed-url");
        _s3Client.Received(1).GetPreSignedURL(Arg.Is<GetPreSignedUrlRequest>(r =>
            r.BucketName == "nimpression-media" &&
            r.Key == "avatars/1.jpg" &&
            r.Expires <= DateTime.UtcNow.AddMinutes(16) &&
            r.Expires >= DateTime.UtcNow.AddMinutes(14)));
    }

    [Fact]
    public async Task DeleteAsync_deletes_object_from_s3()
    {
        using var sut = CreateSut();
        _s3Client.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectResponse());

        await sut.DeleteAsync("nimpression-media", "avatars/1.jpg");

        await _s3Client.Received(1).DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(r => r.BucketName == "nimpression-media" && r.Key == "avatars/1.jpg"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistsAsync_returns_true_when_object_exists()
    {
        using var sut = CreateSut();
        _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectMetadataResponse());

        var exists = await sut.ExistsAsync("nimpression-media", "avatars/1.jpg");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_returns_false_when_not_found()
    {
        using var sut = CreateSut();
        var s3Exception = new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound };
        _s3Client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(s3Exception);

        var exists = await sut.ExistsAsync("nimpression-media", "avatars/missing.jpg");

        exists.Should().BeFalse();
    }
}
