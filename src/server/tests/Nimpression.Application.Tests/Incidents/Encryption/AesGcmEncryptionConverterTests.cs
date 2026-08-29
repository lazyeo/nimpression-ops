using FluentAssertions;
using Nimpression.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Nimpression.Application.Tests.Incidents.Encryption;

public sealed class AesGcmEncryptionConverterTests : IDisposable
{
    private const string ValidTestKey = "k8+1h7T7mK6rL4p5v3z9Q1w2e3r4t5y6u7i8o9p0a1s=";
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
    public void Encrypt_and_Decrypt_roundtrip_returns_original_plaintext()
    {
        // Arrange
        const string plainText = "Rego: ABC123, Name: John Doe, Phone: +64 21 555 1234";

        // Act
        var cipherText = AesGcmEncryptionConverter.Encrypt(plainText);
        var decryptedText = AesGcmEncryptionConverter.Decrypt(cipherText);

        // Assert
        cipherText.Should().NotBeNullOrWhiteSpace();
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
    public void Decrypt_legacy_non_base64_format_returns_as_is()
    {
        // Arrange: 兼容种子数据可能存在的非 base64 文本
        const string legacy = "ENC(ThirdParty_Rego_ABC100_Name_John_Doe)";

        // Act
        var result = AesGcmEncryptionConverter.Decrypt(legacy);

        // Assert
        result.Should().Be(legacy);
    }

    #region 快速失败（Fail-Fast）安全断言

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
