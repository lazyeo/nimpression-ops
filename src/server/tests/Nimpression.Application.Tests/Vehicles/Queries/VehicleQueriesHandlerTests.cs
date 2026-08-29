using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.DTOs;
using Nimpression.Application.Features.Vehicles.Queries.GetActiveVehicleAssignment;
using Nimpression.Application.Features.Vehicles.Queries.GetOdometerReadings;
using Nimpression.Application.Features.Vehicles.Queries.GetVehicleAssignments;
using Nimpression.Application.Features.Vehicles.Queries.GetVehicleById;
using Nimpression.Application.Features.Vehicles.Queries.GetVehiclesList;
using Nimpression.Application.Tests.Vehicles.TestDoubles;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Vehicles.Queries;

public class VehicleQueriesHandlerTests
{
    private readonly FakeVehicleRepository _repository = new();

    [Fact]
    public async Task GetVehicleById_ExistingId_ReturnsDetailDto()
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
            new Kilometres(10000));
        _repository.Vehicles[vehicle.Id] = vehicle;

        var handler = new GetVehicleByIdQueryHandler(_repository);
        var query = new GetVehicleByIdQuery(vehicle.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("ABC123", result.Value.Rego);
        Assert.Equal("Toyota", result.Value.Make);
    }

    [Fact]
    public async Task GetVehicleById_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var handler = new GetVehicleByIdQueryHandler(_repository);
        var query = new GetVehicleByIdQuery(Guid.NewGuid());

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task GetVehiclesList_WithFilter_ReturnsPagedResult()
    {
        // Arrange
        var v1 = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC111"),
            "Toyota",
            "Hilux",
            2022,
            "VIN1",
            new Kilometres(10000),
            new Kilometres(10000),
            status: VehicleStatus.Active);
        var v2 = new Vehicle(
            Guid.NewGuid(),
            new Rego("XYZ222"),
            "Nissan",
            "Navara",
            2021,
            "VIN2",
            new Kilometres(20000),
            new Kilometres(10000),
            status: VehicleStatus.Maintenance);
        _repository.Vehicles[v1.Id] = v1;
        _repository.Vehicles[v2.Id] = v2;

        var handler = new GetVehiclesListQueryHandler(_repository);
        var query = new GetVehiclesListQuery(Search: "Toyota", Status: VehicleStatus.Active);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Single(result.Value.Items);
        Assert.Equal("ABC111", result.Value.Items[0].Rego);
    }

    [Fact]
    public async Task GetActiveVehicleAssignment_ExistingActive_ReturnsAssignmentDto()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var assignmentTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
        var assignment = new VehicleAssignment(Guid.NewGuid(), vehicleId, driverId, assignmentTime, Guid.NewGuid());
        _repository.Assignments[assignment.Id] = assignment;

        var handler = new GetActiveVehicleAssignmentQueryHandler(_repository);
        var query = new GetActiveVehicleAssignmentQuery(vehicleId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(driverId, result.Value!.DriverId);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task GetActiveVehicleAssignment_NoActive_ReturnsNullDtoSuccess()
    {
        // Arrange
        var handler = new GetActiveVehicleAssignmentQueryHandler(_repository);
        var query = new GetActiveVehicleAssignmentQuery(Guid.NewGuid());

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetVehicleAssignments_ReturnsHistoryList()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
        var a1 = new VehicleAssignment(Guid.NewGuid(), vehicleId, Guid.NewGuid(), baseTime.AddDays(-5), Guid.NewGuid());
        var a2 = new VehicleAssignment(Guid.NewGuid(), vehicleId, Guid.NewGuid(), baseTime, Guid.NewGuid());
        _repository.Assignments[a1.Id] = a1;
        _repository.Assignments[a2.Id] = a2;

        var handler = new GetVehicleAssignmentsQueryHandler(_repository);
        var query = new GetVehicleAssignmentsQuery(vehicleId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task GetOdometerReadings_ReturnsReadingsList()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
        var r1 = new OdometerReading(Guid.NewGuid(), vehicleId, Guid.NewGuid(), new Kilometres(10000), "photo1.jpg", baseTime.AddDays(-1), "DriverApp");
        var r2 = new OdometerReading(Guid.NewGuid(), vehicleId, Guid.NewGuid(), new Kilometres(12000), "photo2.jpg", baseTime, "DriverApp");
        _repository.OdometerReadings.Add(r1);
        _repository.OdometerReadings.Add(r2);

        var handler = new GetOdometerReadingsQueryHandler(_repository);
        var query = new GetOdometerReadingsQuery(vehicleId, Limit: 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }
}
