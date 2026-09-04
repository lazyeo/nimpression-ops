using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Nimpression.Infrastructure.Security;
using Xunit;

namespace Nimpression.Application.Tests.Security;

public class JwtSettingsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Validate_WhenSecretIsMissingOrWhitespace_ThrowsInvalidOperationException_WithConfigKeyName(string? secret)
    {
        // Arrange
        var settings = new JwtSettings
        {
            Secret = secret!
        };

        // Act
        var act = () => settings.Validate();

        // Assert
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Jwt__Secret");
        ex.Message.Should().Contain("missing");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("1234567890123456789012345678901")] // 31 bytes
    [InlineData("short_secret_key!")]
    public void Validate_WhenSecretIsShorterThan32Bytes_ThrowsInvalidOperationException_WithLengthAndConfigKeyName(string shortSecret)
    {
        // Arrange
        var settings = new JwtSettings
        {
            Secret = shortSecret
        };

        // Act
        var act = () => settings.Validate();

        // Assert
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Jwt__Secret");
        ex.Message.Should().Contain("at least 32 bytes");
        ex.Message.Should().Contain($"{Encoding.UTF8.GetByteCount(shortSecret)} bytes");
    }

    [Fact]
    public void Validate_WhenSecretIsExactly32Bytes_Succeeds()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Secret = "12345678901234567890123456789012" // Exactly 32 bytes
        };

        // Act
        var act = () => settings.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenSecretIsGreaterThan32Bytes_Succeeds()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Secret = "dev-only-insecure-jwt-secret-never-use-in-production-0000"
        };

        // Act
        var act = () => settings.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void JwtTokenGenerator_Constructor_WhenSecretIsInvalid_FailsFast()
    {
        // Arrange
        var options = Options.Create(new JwtSettings
        {
            Secret = ""
        });

        // Act
        var act = () => new JwtTokenGenerator(options);

        // Assert
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Jwt__Secret");
    }

    [Fact]
    public void JwtTokenGenerator_Constructor_WhenSecretIsShort_FailsFast()
    {
        // Arrange
        var options = Options.Create(new JwtSettings
        {
            Secret = "too_short_key_123"
        });

        // Act
        var act = () => new JwtTokenGenerator(options);

        // Assert
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("Jwt__Secret");
    }
}
