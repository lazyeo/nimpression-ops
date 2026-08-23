namespace Nimpression.Application.Features.Drivers.Storage;

/// <summary>
/// 支持的图片格式枚举。
/// </summary>
public enum ImageFormat
{
    Unknown,
    Jpeg,
    Png
}

/// <summary>
/// 图片校验结果载体。
/// </summary>
public sealed record ImageValidationResult(
    bool IsValid,
    ImageFormat Format,
    string? ContentType,
    string? Extension,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static ImageValidationResult Success(ImageFormat format, string contentType, string extension)
        => new(true, format, contentType, extension, null, null);

    public static ImageValidationResult Unsupported(string message = "Unsupported image format. Only real JPEG and PNG files are allowed.")
        => new(false, ImageFormat.Unknown, null, null, "unsupported_media_type", message);

    public static ImageValidationResult TooLarge(string message = "Image exceeds maximum allowed size of 2MB.")
        => new(false, ImageFormat.Unknown, null, null, "file_too_large", message);
}
