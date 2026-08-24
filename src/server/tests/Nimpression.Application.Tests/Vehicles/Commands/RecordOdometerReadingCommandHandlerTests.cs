using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Commands.RecordOdometerReading;
using Nimpression.Application.Tests.Vehicles.TestDoubles;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Vehicles.Commands;

public class RecordOdometerReadingCommandHandlerTests
{
    private readonly FakeVehicleRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly RecordOdometerReadingCommandHandler _handler;

    public RecordOdometerReadingCommandHandlerTests()
    {
        _handler = new RecordOdometerReadingCommandHandler(_repository, _unitOfWork, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ValidReading_UpdatesVehicleOdometerAndSucceeds()
    {
        // Arrange
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC123"),
            "Toyota",
            "Hilux",
            2022,
            "VIN",
            new Kilometres(10000),
            new Kilometres(10000));
        _repository.Vehicles[vehicle.Id] = vehicle;

        var driverId = Guid.NewGuid();
        _repository.ExistingDriverIds.Add(driverId);

        var command = new RecordOdometerReadingCommand(
            vehicle.Id,
            driverId,
            12500m,
            "photos/odometer1.jpg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.Equal(12500m, vehicle.OdometerKm.Value);
        Assert.Single(_repository.OdometerReadings);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_EqualReading_AllowedAndSucceeds()
    {
        // Arrange
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC123"),
            "Toyota",
            "Hilux",
            2022,
            "VIN",
            new Kilometres(10000),
            new Kilometres(10000));
        _repository.Vehicles[vehicle.Id] = vehicle;

        var driverId = Guid.NewGuid();
        _repository.ExistingDriverIds.Add(driverId);

        var command = new RecordOdometerReadingCommand(
            vehicle.Id,
            driverId,
            10000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10000m, vehicle.OdometerKm.Value);
    }

    [Fact]
    public async Task Handle_DecreasingReading_ReturnsUnprocessable422()
    {
        // Arrange
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC123"),
            "Toyota",
            "Hilux",
            2022,
            "VIN",
            new Kilometres(10000),
            new Kilometres(10000));
        _repository.Vehicles[vehicle.Id] = vehicle;

        var driverId = Guid.NewGuid();
        _repository.ExistingDriverIds.Add(driverId);

        var command = new RecordOdometerReadingCommand(
            vehicle.Id,
            driverId,
            9500m); // Less than 10000

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.UnprocessableEntity, result.Error!.Kind);
        Assert.Equal("odometer_reading_cannot_decrease", result.Error.Code);
        Assert.Equal(10000m, vehicle.OdometerKm.Value);
        Assert.Empty(_repository.OdometerReadings);
    }

    [Fact]
    public async Task Handle_VehicleNotFound_ReturnsNotFound()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        _repository.ExistingDriverIds.Add(driverId);
        var command = new RecordOdometerReadingCommand(Guid.NewGuid(), driverId, 15000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal("vehicle_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Handle_DriverNotFound_ReturnsNotFound()
    {
        // Arrange
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC123"),
            "Toyota",
            "Hilux",
            2022,
            "VIN",
            new Kilometres(10000),
            new Kilometres(10000));
        _repository.Vehicles[vehicle.Id] = vehicle;

        var command = new RecordOdometerReadingCommand(vehicle.Id, Guid.NewGuid(), 15000m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal("driver_not_found", result.Error.Code);
    }
}
