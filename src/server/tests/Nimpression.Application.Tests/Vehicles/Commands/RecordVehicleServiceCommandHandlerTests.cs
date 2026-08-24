using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Commands.RecordVehicleService;
using Nimpression.Application.Tests.Vehicles.TestDoubles;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Vehicles.Commands;

public class RecordVehicleServiceCommandHandlerTests
{
    private readonly FakeVehicleRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordVehicleServiceCommandHandler _handler;

    public RecordVehicleServiceCommandHandlerTests()
    {
        _handler = new RecordVehicleServiceCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidService_UpdatesServiceOdometerAndSucceeds()
    {
        // Arrange
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC123"),
            "Toyota",
            "Hilux",
            2022,
            "VIN",
            new Kilometres(15000),
            new Kilometres(10000),
            new Kilometres(5000));
        _repository.Vehicles[vehicle.Id] = vehicle;

        var command = new RecordVehicleServiceCommand(vehicle.Id, 15000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(15000m, vehicle.LastServiceOdometerKm.Value);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_VehicleNotFound_ReturnsNotFound()
    {
        // Arrange
        var command = new RecordVehicleServiceCommand(Guid.NewGuid(), 10000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task Handle_ServiceKmLessThanLastService_ReturnsValidationError()
    {
        // Arrange
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC123"),
            "Toyota",
            "Hilux",
            2022,
            "VIN",
            new Kilometres(15000),
            new Kilometres(10000),
            new Kilometres(10000));
        _repository.Vehicles[vehicle.Id] = vehicle;

        var command = new RecordVehicleServiceCommand(vehicle.Id, 5000m); // Less than 10000

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.UnprocessableEntity, result.Error!.Kind);
    }
}
