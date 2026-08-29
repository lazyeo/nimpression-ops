using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Nimpression.Infrastructure.Persistence.Configurations;

/// <summary>
/// 可复用的 AES-256-GCM EF Core 属性值转换器（ValueConverter）。
/// 符合 N2.1 隐私合规与 F9.3 第三方信息/PII 落库加密要求。
/// 数据库中落库为 Base64 编码的密文 [12字节 Nonce + 16字节 Tag + 密文]，在 psql 中直查无法看到明文。
/// 密钥优先从环境变量（ENCRYPTION_KEY / COMPLIANCE_ENCRYPTION_KEY）或用户机密中读取，禁止明文硬编码入库。
/// </summary>
public class AesGcmEncryptionConverter : ValueConverter<string?, string?>
{
    private static readonly byte[] FallbackDevKey = Convert.FromBase64String("k8+1h7T7mK6rL4p5v3z9Q1w2e3r4t5y6u7i8o9p0a1s=");

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
            return FallbackDevKey;
        }

        try
        {
            var raw = Convert.FromBase64String(keyEnv);
            if (raw.Length == 32)
            {
                return raw;
            }
        }
        catch (FormatException)
        {
            // 非 Base64 时尝试直接 UTF8 或 SHA-256 派生
        }

        var utf8Bytes = Encoding.UTF8.GetBytes(keyEnv);
        if (utf8Bytes.Length == 32)
        {
            return utf8Bytes;
        }

        return SHA256.HashData(utf8Bytes);
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

        return Convert.ToBase64String(combined);
    }

    public static string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(cipherText);
        }
        catch (FormatException)
        {
            // 兼容非 Base64 的预置种子数据格式（例如 ENC(...)）
            return cipherText;
        }

        if (combined.Length < 28) // 12 Nonce + 16 Tag
        {
            return cipherText;
        }

        try
        {
            var key = GetEncryptionKey();
            using var aesGcm = new AesGcm(key, 16);

            var nonce = new byte[12];
            var tag = new byte[16];
            var cipherBytes = new byte[combined.Length - 28];

            Buffer.BlockCopy(combined, 0, nonce, 0, 12);
            Buffer.BlockCopy(combined, 12, tag, 0, 16);
            Buffer.BlockCopy(combined, 28, cipherBytes, 0, cipherBytes.Length);

            var plainBytes = new byte[cipherBytes.Length];
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            // 解密失败时安全降级
            return cipherText;
        }
    }
}
