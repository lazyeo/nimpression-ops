namespace Nimpression.Application.Features.Drivers.Storage;

/// <summary>
/// 图片魔数（Magic Bytes）与大小校验工具（F2.2）。
/// 严格检查二进制文件头特征，防止修改文件扩展名伪装可执行脚本或恶意文件。
/// </summary>
public static class ImageValidator
{
    public const int MaxSizeBytes = 2 * 1024 * 1024; // 2MB

    // JPEG header: FF D8 FF
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF];

    // PNG header: 89 50 4E 47 0D 0A 1A 0A
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// 基于流的魔数与大小校验。
    /// </summary>
    public static ImageValidationResult Validate(Stream stream, long? declaredLength = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (declaredLength.HasValue && declaredLength.Value > MaxSizeBytes)
        {
            return ImageValidationResult.TooLarge();
        }

        if (stream.CanSeek)
        {
            if (stream.Length > MaxSizeBytes)
            {
                return ImageValidationResult.TooLarge();
            }

            stream.Position = 0;
        }

        var header = new byte[8];
        var bytesRead = stream.Read(header, 0, header.Length);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (bytesRead < 3)
        {
            return ImageValidationResult.Unsupported();
        }

        // 校验 JPEG
        if (header[0] == JpegHeader[0] && header[1] == JpegHeader[1] && header[2] == JpegHeader[2])
        {
            return ImageValidationResult.Success(ImageFormat.Jpeg, "image/jpeg", ".jpg");
        }

        // 校验 PNG
        if (bytesRead >= 8 &&
            header[0] == PngHeader[0] &&
            header[1] == PngHeader[1] &&
            header[2] == PngHeader[2] &&
            header[3] == PngHeader[3] &&
            header[4] == PngHeader[4] &&
            header[5] == PngHeader[5] &&
            header[6] == PngHeader[6] &&
            header[7] == PngHeader[7])
        {
            return ImageValidationResult.Success(ImageFormat.Png, "image/png", ".png");
        }

        return ImageValidationResult.Unsupported();
    }

    /// <summary>
    /// 基于字节数组的魔数与大小校验。
    /// </summary>
    public static ImageValidationResult Validate(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length > MaxSizeBytes)
        {
            return ImageValidationResult.TooLarge();
        }

        if (data.Length < 3)
        {
            return ImageValidationResult.Unsupported();
        }

        if (data[0] == JpegHeader[0] && data[1] == JpegHeader[1] && data[2] == JpegHeader[2])
        {
            return ImageValidationResult.Success(ImageFormat.Jpeg, "image/jpeg", ".jpg");
        }

        if (data.Length >= 8 &&
            data[0] == PngHeader[0] &&
            data[1] == PngHeader[1] &&
            data[2] == PngHeader[2] &&
            data[3] == PngHeader[3] &&
            data[4] == PngHeader[4] &&
            data[5] == PngHeader[5] &&
            data[6] == PngHeader[6] &&
            data[7] == PngHeader[7])
        {
            return ImageValidationResult.Success(ImageFormat.Png, "image/png", ".png");
        }

        return ImageValidationResult.Unsupported();
    }
}
