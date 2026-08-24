using FluentAssertions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Commands.CreateArea;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Xunit;

namespace Nimpression.Application.Tests.Areas.Commands;

public sealed class CreateAreaCommandHandlerTests
{
    private readonly FakeAreaRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly CreateAreaCommandHandler _handler;

    public CreateAreaCommandHandlerTests()
    {
        _handler = new CreateAreaCommandHandler(_repo, _uow);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesAreaAndReturnsId()
    {
        // Arrange
        var command = new CreateAreaCommand(
            "North Shore",
            "AKL-NS",
            "Takapuna, Albany, and Northern corridor",
            "{\"type\":\"Polygon\"}",
            true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        _repo.Areas.Should().ContainKey(result.Value);
        var created = _repo.Areas[result.Value];
        created.Name.Should().Be("North Shore");
        created.Code.Should().Be("AKL-NS");
        created.Description.Should().Be("Takapuna, Albany, and Northern corridor");
        created.IsActive.Should().BeTrue();
        _uow.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicateCode_Returns409Conflict()
    {
        // Arrange
        _uow.ThrowOnSave = true;
        _uow.ExceptionToThrow = new InvalidOperationException("duplicate key value violates unique constraint 23505");

        var command = new CreateAreaCommand(
            "Central Auckland",
            "AKL-CBD",
            "City center");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be("area_code_conflict");
    }

    [Theory]
    [InlineData("", "AKL-CBD")]
    [InlineData("   ", "AKL-CBD")]
    [InlineData("Central", "")]
    [InlineData("Central", "   ")]
    public async Task Handle_EmptyNameOrCode_ReturnsValidationError(string name, string code)
    {
        // Arrange
        var command = new CreateAreaCommand(name, code);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }
}
