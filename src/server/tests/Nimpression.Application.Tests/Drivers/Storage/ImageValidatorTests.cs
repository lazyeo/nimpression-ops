using System.Text;
using FluentAssertions;
using Nimpression.Application.Features.Drivers.Storage;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Storage;

public sealed class ImageValidatorTests
{
    [Fact]
    public void Validate_valid_jpeg_returns_success()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        var result = ImageValidator.Validate(jpegBytes);

        result.IsValid.Should().BeTrue();
        result.Format.Should().Be(ImageFormat.Jpeg);
        result.ContentType.Should().Be("image/jpeg");
        result.Extension.Should().Be(".jpg");
    }

    [Fact]
    public void Validate_valid_png_returns_success()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        var result = ImageValidator.Validate(pngBytes);

        result.IsValid.Should().BeTrue();
        result.Format.Should().Be(ImageFormat.Png);
        result.ContentType.Should().Be("image/png");
        result.Extension.Should().Be(".png");
    }

    [Fact]
    public void Validate_disguised_text_file_with_jpg_extension_is_rejected()
    {
        var fakeJpgBytes = Encoding.UTF8.GetBytes("<html><script>alert('pwn')</script></html>");
        var result = ImageValidator.Validate(fakeJpgBytes);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("unsupported_media_type");
    }

    [Fact]
    public void Validate_disguised_exe_file_is_rejected()
    {
        var exeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 }; // MZ header
        var result = ImageValidator.Validate(exeBytes);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("unsupported_media_type");
    }

    [Fact]
    public void Validate_oversized_file_is_rejected()
    {
        var oversizedStream = new MemoryStream(new byte[ImageValidator.MaxSizeBytes + 10]);
        oversizedStream.Write([0xFF, 0xD8, 0xFF]);
        oversizedStream.Position = 0;

        var result = ImageValidator.Validate(oversizedStream);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be("file_too_large");
    }

    [Fact]
    public void Validate_stream_restores_stream_position()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        using var stream = new MemoryStream(jpegBytes);

        var result = ImageValidator.Validate(stream);

        result.IsValid.Should().BeTrue();
        stream.Position.Should().Be(0);
    }
}
