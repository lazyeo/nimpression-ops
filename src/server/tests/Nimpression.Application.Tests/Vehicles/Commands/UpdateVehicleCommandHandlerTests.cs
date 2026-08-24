using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Commands.UpdateVehicle;
using Nimpression.Application.Features.Vehicles.Commands.UpdateVehicleStatus;
using Nimpression.Application.Tests.Vehicles.TestDoubles;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Vehicles.Commands;

public class UpdateVehicleCommandHandlerTests
{
    private readonly FakeVehicleRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateVehicleCommandHandler _updateHandler;
    private readonly UpdateVehicleStatusCommandHandler _statusHandler;

    public UpdateVehicleCommandHandlerTests()
    {
        _updateHandler = new UpdateVehicleCommandHandler(_repository, _unitOfWork);
        _statusHandler = new UpdateVehicleStatusCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_UpdateExistingVehicle_UpdatesDatesAndSucceeds()
    {
        // Arrange
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC123"),
            "Toyota",
            "Hilux",
            2022,
            "VIN",
            new Kilometres(5000),
            new Kilometres(10000));
        _repository.Vehicles[vehicle.Id] = vehicle;

        var command = new UpdateVehicleCommand(
            vehicle.Id,
            new DateOnly(2027, 6, 1),
            new DateOnly(2027, 6, 1),
            new DateOnly(2027, 6, 1),
            VehicleStatus.Active);

        // Act
        var result = await _updateHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2027, 6, 1), vehicle.WofExpiry);
        Assert.Equal(new DateOnly(2027, 6, 1), vehicle.CofExpiry);
        Assert.Equal(new DateOnly(2027, 6, 1), vehicle.InsuranceExpiry);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_UpdateNonExistentVehicle_ReturnsNotFound()
    {
        // Arrange
        var command = new UpdateVehicleCommand(
            Guid.NewGuid(),
            null,
            null,
            null,
            VehicleStatus.Active);

        // Act
        var result = await _updateHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task Handle_UpdateStatus_ChangesVehicleStatus()
    {
        // Arrange
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC123"),
            "Toyota",
            "Hilux",
            2022,
            "VIN",
            new Kilometres(5000),
            new Kilometres(10000),
            status: VehicleStatus.Active);
        _repository.Vehicles[vehicle.Id] = vehicle;

        var command = new UpdateVehicleStatusCommand(vehicle.Id, VehicleStatus.Maintenance);

        // Act
        var result = await _statusHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Maintenance, vehicle.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_UpdateStatusForNonExistentVehicle_ReturnsNotFound()
    {
        // Arrange
        var command = new UpdateVehicleStatusCommand(Guid.NewGuid(), VehicleStatus.Decommissioned);

        // Act
        var result = await _statusHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }
}
