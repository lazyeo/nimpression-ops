using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Commands.CreateVehicle;
using Nimpression.Application.Tests.Vehicles.TestDoubles;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Vehicles.Commands;

public class CreateVehicleCommandHandlerTests
{
    private readonly FakeVehicleRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateVehicleCommandHandler _handler;

    public CreateVehicleCommandHandlerTests()
    {
        _handler = new CreateVehicleCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesVehicleAndReturnsId()
    {
        // Arrange
        var command = new CreateVehicleCommand(
            "ABC123",
            "Toyota",
            "Hilux",
            2023,
            "ENC_VIN_123",
            10000m,
            15000m,
            0m,
            new DateOnly(2027, 1, 1),
            null,
            new DateOnly(2027, 1, 1),
            VehicleStatus.Active);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.True(_repository.Vehicles.ContainsKey(result.Value));
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_DuplicateRego_ReturnsConflictError()
    {
        // Arrange
        var rego = new Rego("ABC123");
        var existing = new Vehicle(
            Guid.NewGuid(),
            rego,
            "Toyota",
            "Hilux",
            2022,
            "VIN",
            new Kilometres(5000),
            new Kilometres(10000));
        _repository.Vehicles[existing.Id] = existing;

        var command = new CreateVehicleCommand(
            "ABC123",
            "Toyota",
            "Hilux",
            2023,
            "VIN2",
            10000m,
            15000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal("vehicle_rego_conflict", result.Error.Code);
    }

    [Fact]
    public async Task Handle_InvalidRegoFormat_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateVehicleCommand(
            "", // Invalid
            "Toyota",
            "Hilux",
            2023,
            "VIN",
            10000m,
            15000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }
}
