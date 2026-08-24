using FluentAssertions;
using Nimpression.Application.Features.Identity.Commands.Login;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Commands;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Theory]
    [InlineData("valid@example.com", "Password123!")]
    [InlineData("admin.user@nimpression.co.nz", "SuperSecretPass!")]
    public void Validate_WithValidInputs_IsValid(string email, string password)
    {
        var command = new LoginCommand(email, password);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Password123!")]
    [InlineData(null, "Password123!")]
    [InlineData("   ", "Password123!")]
    public void Validate_WithEmptyEmail_IsInvalid(string? email, string password)
    {
        var command = new LoginCommand(email!, password);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Theory]
    [InlineData("test@example.com", "")]
    [InlineData("test@example.com", null)]
    [InlineData("test@example.com", "   ")]
    public void Validate_WithEmptyPassword_IsInvalid(string email, string? password)
    {
        var command = new LoginCommand(email, password!);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}
