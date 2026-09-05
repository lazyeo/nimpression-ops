using System.Security.Cryptography;
using FluentAssertions;
using Nimpression.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Nimpression.Application.Tests.Incidents.Encryption;

public sealed class AesGcmEncryptionConverterTests : IDisposable
{
    private const string ValidTestKey = "ZGV2LW9ubHktaW5zZWN1cmUtYWVzLWtleS0zMmJ5dGU=";
    private const string OtherValidKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private readonly string? _originalKey;

    public AesGcmEncryptionConverterTests()
    {
        _originalKey = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", ValidTestKey);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", _originalKey);
    }

    [Fact]
    public void Encrypt_and_Decrypt_roundtrip_returns_original_plaintext_and_has_prefix()
    {
        // Arrange
        const string plainText = "Rego: ABC123, Name: John Doe, Phone: +64 21 555 1234";

        // Act
        var cipherText = AesGcmEncryptionConverter.Encrypt(plainText);
        var decryptedText = AesGcmEncryptionConverter.Decrypt(cipherText);

        // Assert
        cipherText.Should().NotBeNullOrWhiteSpace();
        cipherText.Should().StartWith(AesGcmEncryptionConverter.CiphertextPrefix);
        cipherText.Should().NotBe(plainText);
        decryptedText.Should().Be(plainText);
    }

    [Fact]
    public void Encrypt_produces_different_ciphertexts_due_to_random_nonce()
    {
        // Arrange
        const string plainText = "Identical message encrypted twice";

        // Act
        var cipher1 = AesGcmEncryptionConverter.Encrypt(plainText);
        var cipher2 = AesGcmEncryptionConverter.Encrypt(plainText);

        // Assert: 即使明文相同，由于 12 字节 Nonce 随机生成，密文必须不同
        cipher1.Should().NotBe(cipher2);
        cipher1.Should().StartWith(AesGcmEncryptionConverter.CiphertextPrefix);
        cipher2.Should().StartWith(AesGcmEncryptionConverter.CiphertextPrefix);
        AesGcmEncryptionConverter.Decrypt(cipher1).Should().Be(plainText);
        AesGcmEncryptionConverter.Decrypt(cipher2).Should().Be(plainText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Encrypt_and_Decrypt_handles_null_or_empty_gracefully(string? input)
    {
        AesGcmEncryptionConverter.Encrypt(input).Should().Be(input);
        AesGcmEncryptionConverter.Decrypt(input).Should().Be(input);
    }

    [Fact]
    public void Decrypt_unencrypted_legacy_data_without_prefix_returns_as_is()
    {
        // Arrange: 显式没有 "enc:v1:" 前缀的未加密历史/种子数据原样返回
        const string legacyPlainText = "ThirdParty_Rego_ABC100_Name_John_Doe";

        // Act
        var result = AesGcmEncryptionConverter.Decrypt(legacyPlainText);

        // Assert
        result.Should().Be(legacyPlainText);
    }

    #region 解密失败抛异常（拒绝静默降级）

    [Fact]
    public void Decrypt_with_wrong_key_throws_CryptographicException()
    {
        // Arrange: 使用 Key A 加密
        const string plainText = "Sensitive PII Data";
        var cipherText = AesGcmEncryptionConverter.Encrypt(plainText);

        // Act: 切换为 Key B 进行解密
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", OtherValidKey);

        // Assert: 密钥不匹配时必须抛出 CryptographicException，绝对禁止静默返回密文字符串
        var act = () => AesGcmEncryptionConverter.Decrypt(cipherText);
        act.Should().Throw<CryptographicException>()
            .WithMessage("*decryption failed*");
    }

    [Fact]
    public void Decrypt_corrupted_base64_payload_throws_CryptographicException()
    {
        // Arrange: 带有 enc:v1: 前缀但是 Base64 损坏的数据
        const string corrupted = "enc:v1:@@@invalid-base64-payload@@@";

        // Act & Assert
        var act = () => AesGcmEncryptionConverter.Decrypt(corrupted);
        act.Should().Throw<CryptographicException>()
            .WithMessage("*Failed to decode base64*");
    }

    [Fact]
    public void Decrypt_truncated_payload_throws_CryptographicException()
    {
        // Arrange: 带有 enc:v1: 前缀但是长度小于 28 字节的数据（例如只有 10 字节）
        var shortBase64 = Convert.ToBase64String(new byte[10]);
        var truncated = $"{AesGcmEncryptionConverter.CiphertextPrefix}{shortBase64}";

        // Act & Assert
        var act = () => AesGcmEncryptionConverter.Decrypt(truncated);
        act.Should().Throw<CryptographicException>()
            .WithMessage("*corrupted or truncated*");
    }

    #endregion

    #region 密钥配置快速失败（Fail-Fast）断言

    [Fact]
    public void GetEncryptionKey_throws_InvalidOperationException_when_key_is_missing()
    {
        // Arrange: 临时清空全部环境变量
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", null);
        Environment.SetEnvironmentVariable("COMPLIANCE_ENCRYPTION_KEY", null);
        Environment.SetEnvironmentVariable("AES_256_KEY", null);

        // Act & Assert: 缺失密钥必须立即抛异常快速失败，禁止静默使用公开硬编码密钥
        var act = () => AesGcmEncryptionConverter.GetEncryptionKey();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AES-256-GCM encryption key is missing*");
    }

    [Fact]
    public void GetEncryptionKey_throws_InvalidOperationException_when_key_is_not_valid_base64()
    {
        // Arrange: 设置非 Base64 格式字符串
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", "not-a-valid-base64-string!@#$");

        // Act & Assert: 格式不合法直接报错，禁止猜测或通过弱 hash 隐式派生
        var act = () => AesGcmEncryptionConverter.GetEncryptionKey();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be a valid Base64-encoded string*");
    }

    [Fact]
    public void GetEncryptionKey_throws_InvalidOperationException_when_key_length_is_not_32_bytes()
    {
        // Arrange: 设置仅 16 字节的 Base64 密钥
        var shortKey = Convert.ToBase64String(new byte[16]);
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", shortKey);

        // Act & Assert: 长度不足 32 字节直接报错
        var act = () => AesGcmEncryptionConverter.GetEncryptionKey();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be exactly 32 bytes (256 bits)*");
    }

    #endregion
}
