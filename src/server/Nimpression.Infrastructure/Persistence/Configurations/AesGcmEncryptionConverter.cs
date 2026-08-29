using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Nimpression.Infrastructure.Persistence.Configurations;

/// <summary>
/// 可复用的 AES-256-GCM EF Core 属性值转换器（ValueConverter）。
/// 符合 N2.1 隐私合规与 F9.3 第三方信息/PII 落库加密要求。
/// 数据库中落库为显式版本前缀 + Base64 编码的密文 [enc:v1:Base64(12字节 Nonce + 16字节 Tag + 密文)]，在 psql 中直查无法看到明文。
/// 显式前缀机制：
/// 1. 带有 "enc:v1:" 前缀的字段属于加密数据，解密失败（篡改、密钥错误、数据截断）时直接抛出异常，绝不静默降级为明文；
/// 2. 无前缀的数据作为未加密历史/种子数据原样读取，兼容性显式、透明且可被 SQL 明确统计。
/// </summary>
public class AesGcmEncryptionConverter : ValueConverter<string?, string?>
{
    public const string CiphertextPrefix = "enc:v1:";

    public AesGcmEncryptionConverter()
        : base(
            v => Encrypt(v),
            v => Decrypt(v))
    {
    }

    public static byte[] GetEncryptionKey()
    {
        var keyEnv = Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
            ?? Environment.GetEnvironmentVariable("COMPLIANCE_ENCRYPTION_KEY")
            ?? Environment.GetEnvironmentVariable("AES_256_KEY");

        if (string.IsNullOrWhiteSpace(keyEnv))
        {
            throw new InvalidOperationException(
                "AES-256-GCM encryption key is missing. Please configure the 'ENCRYPTION_KEY' environment variable or user-secrets with a valid Base64-encoded 32-byte key.");
        }

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(keyEnv.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "AES-256-GCM encryption key is invalid. Key must be a valid Base64-encoded string.", ex);
        }

        if (raw.Length != 32)
        {
            throw new InvalidOperationException(
                $"AES-256-GCM encryption key must be exactly 32 bytes (256 bits), but got {raw.Length} bytes.");
        }

        return raw;
    }

    public static string? Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        var key = GetEncryptionKey();
        using var aesGcm = new AesGcm(key, 16);

        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length + tag.Length, cipherBytes.Length);

        return CiphertextPrefix + Convert.ToBase64String(combined);
    }

    public static string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        // 无显式加密前缀的字符串视为未加密历史明文，原样返回
        if (!cipherText.StartsWith(CiphertextPrefix, StringComparison.Ordinal))
        {
            return cipherText;
        }

        var base64 = cipherText[CiphertextPrefix.Length..];
        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Failed to decode base64 ciphertext payload.", ex);
        }

        if (combined.Length < 28) // 12 Nonce + 16 Tag
        {
            throw new CryptographicException(
                $"Ciphertext payload is corrupted or truncated ({combined.Length} bytes). Expected at least 28 bytes for nonce and auth tag.");
        }

        var key = GetEncryptionKey();
        using var aesGcm = new AesGcm(key, 16);

        var nonce = new byte[12];
        var tag = new byte[16];
        var cipherBytes = new byte[combined.Length - 28];

        Buffer.BlockCopy(combined, 0, nonce, 0, 12);
        Buffer.BlockCopy(combined, 12, tag, 0, 16);
        Buffer.BlockCopy(combined, 28, cipherBytes, 0, cipherBytes.Length);

        var plainBytes = new byte[cipherBytes.Length];
        try
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "AES-256-GCM decryption failed: authentication tag verification mismatch or incorrect decryption key.", ex);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }
}
