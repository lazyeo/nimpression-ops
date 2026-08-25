using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Constraints;

[Collection("PostgreSqlCollection")]
public class VehicleConstraintTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public VehicleConstraintTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Vehicle_DuplicateRego_ThrowsDbUpdateException()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var uniqueRego = TestDataFactory.CreateRego("TST");

        var v1 = new Vehicle(
            Guid.NewGuid(),
            new Rego(uniqueRego),
            "Isuzu",
            "NPR",
            2022,
            "ENC(VIN1)",
            new Kilometres(10000),
            new Kilometres(10000),
            new Kilometres(0),
            null,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 1),
            VehicleStatus.Active);

        var v2 = new Vehicle(
            Guid.NewGuid(),
            new Rego(uniqueRego), // Same Rego
            "Hino",
            "300",
            2023,
            "ENC(VIN2)",
            new Kilometres(5000),
            new Kilometres(10000),
            new Kilometres(0),
            null,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 1),
            VehicleStatus.Active);

        await context.Vehicles.AddAsync(v1);
        await context.SaveChangesAsync();

        // Act
        await context.Vehicles.AddAsync(v2);
        var act = () => context.SaveChangesAsync();

        // Assert: Database unique index rejects duplicate Rego
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task VehicleAssignment_ConcurrentUnreleasedAssignmentsForSameVehicle_ThrowsDbUpdateException()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();

        // Create prerequisites: 2 Driver Users, 1 Dispatcher User, 2 Drivers, 1 Vehicle
        var baseNow = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var dispatcher = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("dispatcher"),
            "hash",
            UserRole.Dispatcher,
            "Dispatcher Test",
            "en-NZ",
            baseNow);

        var driverUser1 = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver1"),
            "hash",
            UserRole.Driver,
            "Driver 1 Test",
            "en-NZ",
            baseNow);

        var driverUser2 = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver2"),
            "hash",
            UserRole.Driver,
            "Driver 2 Test",
            "en-NZ",
            baseNow);

        await context.Users.AddRangeAsync(dispatcher, driverUser1, driverUser2);

        var d1 = new Driver(
            Guid.NewGuid(),
            driverUser1.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "ENC(phone)",
            "ENC(addr)",
            "ENC(emg)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        var d2 = new Driver(
            Guid.NewGuid(),
            driverUser2.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "ENC(phone)",
            "ENC(addr)",
            "ENC(emg)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        await context.Drivers.AddRangeAsync(d1, d2);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("U"),
            "Fuso",
            "Canter",
            2021,
            "ENC(VIN)",
            new Kilometres(20000),
            new Kilometres(10000),
            new Kilometres(10000),
            null,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 1),
            VehicleStatus.Active);
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        // First active assignment (ReleasedAt is null)
        var assignment1 = new VehicleAssignment(
            Guid.NewGuid(),
            vehicle.Id,
            d1.Id,
            baseNow.AddDays(-1),
            dispatcher.Id);
        await context.VehicleAssignments.AddAsync(assignment1);
        await context.SaveChangesAsync();

        // Act: Attempting second active assignment for the same vehicle (ReleasedAt is null)
        var assignment2 = new VehicleAssignment(
            Guid.NewGuid(),
            vehicle.Id,
            d2.Id,
            baseNow,
            dispatcher.Id);
        await context.VehicleAssignments.AddAsync(assignment2);
        var act = () => context.SaveChangesAsync();

        // Assert: Database partial unique index WHERE ReleasedAt IS NULL rejects concurrent assignment
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task VehicleAssignment_ReleasedAssignment_AllowsNewAssignmentForSameVehicle()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();

        var baseNow = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var dispatcher = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("dispatcher"),
            "hash",
            UserRole.Dispatcher,
            "Dispatcher Test 2",
            "en-NZ",
            baseNow);

        var driverUser1 = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver1"),
            "hash",
            UserRole.Driver,
            "Driver 1 Test 2",
            "en-NZ",
            baseNow);

        var driverUser2 = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver2"),
            "hash",
            UserRole.Driver,
            "Driver 2 Test 2",
            "en-NZ",
            baseNow);

        await context.Users.AddRangeAsync(dispatcher, driverUser1, driverUser2);

        var d1 = new Driver(
            Guid.NewGuid(),
            driverUser1.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "ENC(phone)",
            "ENC(addr)",
            "ENC(emg)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        var d2 = new Driver(
            Guid.NewGuid(),
            driverUser2.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "ENC(phone)",
            "ENC(addr)",
            "ENC(emg)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        await context.Drivers.AddRangeAsync(d1, d2);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("R"),
            "Fuso",
            "Canter",
            2021,
            "ENC(VIN)",
            new Kilometres(20000),
            new Kilometres(10000),
            new Kilometres(10000),
            null,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 1),
            VehicleStatus.Active);
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        // Past assignment that is released
        var pastAssignment = new VehicleAssignment(
            Guid.NewGuid(),
            vehicle.Id,
            d1.Id,
            baseNow.AddDays(-10),
            dispatcher.Id);
        pastAssignment.Release(baseNow.AddDays(-2));
        await context.VehicleAssignments.AddAsync(pastAssignment);
        await context.SaveChangesAsync();

        // Act: New active assignment for the same vehicle
        var currentAssignment = new VehicleAssignment(
            Guid.NewGuid(),
            vehicle.Id,
            d2.Id,
            baseNow,
            dispatcher.Id);
        await context.VehicleAssignments.AddAsync(currentAssignment);
        var act = () => context.SaveChangesAsync();

        // Assert: Succeeds because the previous assignment was released
        await act.Should().NotThrowAsync();
    }
}
