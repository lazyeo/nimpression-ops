using FluentAssertions;
using Nimpression.Application.Features.Identity.Commands.ChangePassword;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Commands;

public class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValid12CharPassword_IsValid()
    {
        var command = new ChangePasswordCommand(Guid.NewGuid(), "OldPassword123!", "NewSecurePassword123!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("12345678901")] // 11 chars
    [InlineData("ShortPass!")]  // 10 chars
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithPasswordShorterThan12Chars_IsInvalid(string? newPassword)
    {
        var command = new ChangePasswordCommand(Guid.NewGuid(), "OldPassword123!", newPassword!);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void Validate_WithEmptyUserId_IsInvalid()
    {
        var command = new ChangePasswordCommand(Guid.Empty, "OldPassword123!", "NewSecurePassword123!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }
}
