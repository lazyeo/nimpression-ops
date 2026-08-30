using FluentAssertions;
using FluentValidation.TestHelper;
using Nimpression.Application.Features.News.Commands.CreateNewsPost;
using Nimpression.Domain.Enums;
using Xunit;

namespace Nimpression.Application.Tests.News.Commands;

public class CreateNewsPostCommandValidatorTests
{
    private readonly CreateNewsPostCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        var command = new CreateNewsPostCommand("Title", "Body En", "Body Zh", NewsAudience.All);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyTitle_HasValidationError(string? title)
    {
        var command = new CreateNewsPostCommand(title!, "Body En", "Body Zh", NewsAudience.All);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_TitleExceeding200Chars_HasValidationError()
    {
        var command = new CreateNewsPostCommand(new string('a', 201), "Body En", "Body Zh", NewsAudience.All);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyBodyEn_HasValidationError(string? bodyEn)
    {
        var command = new CreateNewsPostCommand("Title", bodyEn!, "Body Zh", NewsAudience.All);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.BodyEn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyBodyZh_HasValidationError(string? bodyZh)
    {
        var command = new CreateNewsPostCommand("Title", "Body En", bodyZh!, NewsAudience.All);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.BodyZh);
    }

    [Fact]
    public void Validate_InvalidAudience_HasValidationError()
    {
        var command = new CreateNewsPostCommand("Title", "Body En", "Body Zh", (NewsAudience)999);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Audience);
    }
}
