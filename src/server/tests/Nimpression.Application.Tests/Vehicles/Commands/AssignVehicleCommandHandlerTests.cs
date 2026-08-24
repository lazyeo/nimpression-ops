using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Commands.AssignVehicle;
using Nimpression.Application.Features.Vehicles.Commands.ReleaseVehicleAssignment;
using Nimpression.Application.Tests.Vehicles.TestDoubles;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Vehicles.Commands;

public class AssignVehicleCommandHandlerTests
{
    private readonly FakeVehicleRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly AssignVehicleCommandHandler _assignHandler;
    private readonly ReleaseVehicleAssignmentCommandHandler _releaseHandler;

    public AssignVehicleCommandHandlerTests()
    {
        _assignHandler = new AssignVehicleCommandHandler(_repository, _unitOfWork, _currentUser, _dateTimeProvider);
        _releaseHandler = new ReleaseVehicleAssignmentCommandHandler(_repository, _unitOfWork, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ValidAssignment_CreatesAssignmentAndReturnsId()
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

        var driverId = Guid.NewGuid();
        _repository.ExistingDriverIds.Add(driverId);

        var command = new AssignVehicleCommand(vehicle.Id, driverId);

        // Act
        var result = await _assignHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.True(_repository.Assignments.ContainsKey(result.Value));
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_VehicleNotFound_ReturnsNotFound()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        _repository.ExistingDriverIds.Add(driverId);
        var command = new AssignVehicleCommand(Guid.NewGuid(), driverId);

        // Act
        var result = await _assignHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal("vehicle_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Handle_DecommissionedVehicle_ReturnsUnprocessable()
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
            status: VehicleStatus.Decommissioned);
        _repository.Vehicles[vehicle.Id] = vehicle;

        var driverId = Guid.NewGuid();
        _repository.ExistingDriverIds.Add(driverId);
        var command = new AssignVehicleCommand(vehicle.Id, driverId);

        // Act
        var result = await _assignHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.UnprocessableEntity, result.Error!.Kind);
        Assert.Equal("vehicle_decommissioned", result.Error.Code);
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
            new Kilometres(5000),
            new Kilometres(10000));
        _repository.Vehicles[vehicle.Id] = vehicle;

        var command = new AssignVehicleCommand(vehicle.Id, Guid.NewGuid());

        // Act
        var result = await _assignHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal("driver_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ReturnsUnauthorized()
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

        var driverId = Guid.NewGuid();
        _repository.ExistingDriverIds.Add(driverId);
        _currentUser.UserId = null;

        var command = new AssignVehicleCommand(vehicle.Id, driverId);

        // Act
        var result = await _assignHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Unauthorized, result.Error!.Kind);
    }

    [Fact]
    public async Task Handle_DbUniqueConstraintViolation_ReturnsConflict409()
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

        var driverId = Guid.NewGuid();
        _repository.ExistingDriverIds.Add(driverId);

        // Simulate database unique constraint exception on save (SqlState 23505)
        _unitOfWork.ThrowOnSave = true;
        _unitOfWork.ExceptionToThrow = new InvalidOperationException("duplicate key value violates unique constraint \"IX_VehicleAssignments_VehicleId_Active\" (SqlState: 23505)");

        var command = new AssignVehicleCommand(vehicle.Id, driverId);

        // Act
        var result = await _assignHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal("vehicle_already_assigned", result.Error.Code);
    }

    [Fact]
    public async Task Handle_ReleaseActiveAssignment_SetsReleasedAtAndSucceeds()
    {
        // Arrange
        var assignment = new VehicleAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-1),
            Guid.NewGuid());
        _repository.Assignments[assignment.Id] = assignment;

        var command = new ReleaseVehicleAssignmentCommand(assignment.Id);

        // Act
        var result = await _releaseHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(assignment.IsActive);
        Assert.NotNull(assignment.ReleasedAt);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Handle_ReleaseAlreadyReleasedAssignment_ReturnsUnprocessable()
    {
        // Arrange
        var assignment = new VehicleAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-2),
            Guid.NewGuid());
        assignment.Release(DateTimeOffset.UtcNow.AddDays(-1));
        _repository.Assignments[assignment.Id] = assignment;

        var command = new ReleaseVehicleAssignmentCommand(assignment.Id);

        // Act
        var result = await _releaseHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.UnprocessableEntity, result.Error!.Kind);
        Assert.Equal("assignment_already_released", result.Error.Code);
    }
}
