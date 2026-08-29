using FluentAssertions;
using Nimpression.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Nimpression.Application.Tests.Incidents.Encryption;

public sealed class AesGcmEncryptionConverterTests
{
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
}
