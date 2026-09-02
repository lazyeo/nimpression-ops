using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Commands.AssignVehicle;
using Nimpression.Application.Features.Vehicles.Commands.CreateVehicle;
using Nimpression.Application.Features.Vehicles.Commands.RecordOdometerReading;
using Nimpression.Application.Features.Vehicles.Commands.RecordVehicleService;
using Nimpression.Application.Features.Vehicles.Commands.ReleaseVehicleAssignment;
using Nimpression.Application.Features.Vehicles.Commands.UpdateVehicle;
using Nimpression.Application.Features.Vehicles.Commands.UpdateVehicleStatus;
using Nimpression.Application.Features.Vehicles.DTOs;
using Nimpression.Application.Features.Vehicles.Queries.GetActiveVehicleAssignment;
using Nimpression.Application.Features.Vehicles.Queries.GetOdometerReadings;
using Nimpression.Application.Features.Vehicles.Queries.GetVehicleAssignments;
using Nimpression.Application.Features.Vehicles.Queries.GetVehicleById;
using Nimpression.Application.Features.Vehicles.Queries.GetVehiclesList;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Vehicles.Persistence;
using Xunit;

namespace Nimpression.Integration.Tests.Vehicles;

[Collection("PostgreSqlCollection")]
public class VehicleIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public VehicleIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(User Dispatcher, Driver Driver1, Driver Driver2)> SeedPrerequisitesAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var baseNow = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var dispatcher = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("dispatcher"),
            "hash",
            UserRole.Dispatcher,
            "Fleet Dispatcher",
            "en-NZ",
            baseNow);

        var driverUser1 = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver1"),
            "hash",
            UserRole.Driver,
            "Driver One",
            "en-NZ",
            baseNow);

        var driverUser2 = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver2"),
            "hash",
            UserRole.Driver,
            "Driver Two",
            "en-NZ",
            baseNow);

        await context.Users.AddRangeAsync(dispatcher, driverUser1, driverUser2);

        var driver1 = new Driver(
            Guid.NewGuid(),
            driverUser1.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(32m),
            new Money(45m),
            new Money(0.85m),
            "ENC(021123456)",
            "ENC(123 Auckland Rd)",
            "ENC(Emergency Contact)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        var driver2 = new Driver(
            Guid.NewGuid(),
            driverUser2.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(32m),
            new Money(45m),
            new Money(0.85m),
            "ENC(021654321)",
            "ENC(456 Wellington St)",
            "ENC(Emergency Contact 2)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        await context.Drivers.AddRangeAsync(driver1, driver2);
        await context.SaveChangesAsync();

        return (dispatcher, driver1, driver2);
    }

    #region F3.1 车辆 CRUD 测试

    [Fact]
    public async Task F3_1_CreateVehicle_Success_PersistsToDatabase()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new EfVehicleRepository(context);
        var uow = new EfUnitOfWork(context);
        var handler = new CreateVehicleCommandHandler(repo, uow);

        var regoStr = TestDataFactory.CreateRego("V");
        var command = new CreateVehicleCommand(
            regoStr,
            "Isuzu",
            "NPR 250",
            2023,
            "ENC(VIN_TEST_1)",
            15000m,
            10000m,
            10000m,
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 1),
            VehicleStatus.Active);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        await using var verifyContext = _fixture.CreateDbContext();
        var saved = await verifyContext.Vehicles.FindAsync(result.Value);
        saved.Should().NotBeNull();
        saved!.Rego.Value.Should().Be(regoStr);
        saved.Make.Should().Be("Isuzu");
        saved.Model.Should().Be("NPR 250");
    }

    [Fact]
    public async Task F3_1_CreateVehicle_DuplicateRego_Returns409Conflict()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new EfVehicleRepository(context);
        var uow = new EfUnitOfWork(context);
        var handler = new CreateVehicleCommandHandler(repo, uow);

        var duplicateRego = TestDataFactory.CreateRego("DUP");

        var command1 = new CreateVehicleCommand(
            duplicateRego,
            "Toyota",
            "Hilux",
            2022,
            "ENC(VIN_DUP_1)",
            10000m,
            15000m);

        var command2 = new CreateVehicleCommand(
            duplicateRego,
            "Ford",
            "Ranger",
            2023,
            "ENC(VIN_DUP_2)",
            20000m,
            15000m);

        // Act: First creation succeeds
        var result1 = await handler.Handle(command1, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        // Act: Second creation with duplicate Rego returns 409 Conflict
        var result2 = await handler.Handle(command2, CancellationToken.None);

        // Assert
        result2.IsSuccess.Should().BeFalse();
        result2.Error.Should().NotBeNull();
        result2.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result2.Error.Code.Should().Be("vehicle_rego_conflict");
    }

    [Fact]
    public async Task F3_1_UpdateVehicleAndStatus_UpdatesFieldsSuccessfully()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new EfVehicleRepository(context);
        var uow = new EfUnitOfWork(context);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("U"),
            "Hino",
            "300",
            2021,
            "ENC(VIN)",
            new Kilometres(30000),
            new Kilometres(15000));
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        var updateHandler = new UpdateVehicleCommandHandler(repo, uow);
        var statusHandler = new UpdateVehicleStatusCommandHandler(repo, uow);

        var newWof = new DateOnly(2027, 8, 1);
        var updateCommand = new UpdateVehicleCommand(
            vehicle.Id,
            newWof,
            newWof,
            newWof,
            VehicleStatus.Maintenance);

        // Act
        var updateResult = await updateHandler.Handle(updateCommand, CancellationToken.None);
        updateResult.IsSuccess.Should().BeTrue();

        var statusResult = await statusHandler.Handle(
            new UpdateVehicleStatusCommand(vehicle.Id, VehicleStatus.Active),
            CancellationToken.None);
        statusResult.IsSuccess.Should().BeTrue();

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        var updated = await verifyContext.Vehicles.FindAsync(vehicle.Id);
        updated!.WofExpiry.Should().Be(newWof);
        updated.Status.Should().Be(VehicleStatus.Active);
    }

    [Fact]
    public async Task F3_1_RecordVehicleService_UpdatesLastServiceOdometer()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new EfVehicleRepository(context);
        var uow = new EfUnitOfWork(context);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("S"),
            "Fuso",
            "Canter",
            2022,
            "ENC(VIN)",
            new Kilometres(25000),
            new Kilometres(10000),
            new Kilometres(10000));
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        var handler = new RecordVehicleServiceCommandHandler(repo, uow);
        var command = new RecordVehicleServiceCommand(vehicle.Id, 25000m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verifyContext = _fixture.CreateDbContext();
        var updated = await verifyContext.Vehicles.FindAsync(vehicle.Id);
        updated!.LastServiceOdometerKm.Value.Should().Be(25000m);
        updated.DistanceSinceLastService.Value.Should().Be(0m);
    }

    #endregion

    #region F3.2 车辆分派与真实并发测试

    /// <summary>
    /// F3.2 关键验收标准：
    /// 真实并发测试：使用两个独立的 DbContext 实例模拟两个并发请求同时向同一辆车插入未释放分派。
    /// 一个成功（201/Guid），另一个捕获 PostgreSQL 唯一约束冲突（SqlState 23505）并返回 409 Conflict。
    /// 禁止先查后写，通过底层部分唯一索引 WHERE "ReleasedAt" IS NULL 保证排他性。
    /// </summary>
    [Fact]
    public async Task F3_2_ConcurrentAssignments_OneSucceeds_OneReturns409Conflict()
    {
        // Arrange
        var (dispatcher, driver1, driver2) = await SeedPrerequisitesAsync();

        await using var setupContext = _fixture.CreateDbContext();
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("C"),
            "Isuzu",
            "Giga",
            2023,
            "ENC(VIN_CONCURRENT)",
            new Kilometres(50000),
            new Kilometres(20000));
        await setupContext.Vehicles.AddAsync(vehicle);
        await setupContext.SaveChangesAsync();

        // 创建两个独立的 DbContext 实例模拟真实并发
        await using var contextA = _fixture.CreateDbContext();
        await using var contextB = _fixture.CreateDbContext();

        var repoA = new EfVehicleRepository(contextA);
        var uowA = new EfUnitOfWork(contextA);
        var currentUserA = new TestCurrentUser(dispatcher.Id);
        var dtProviderA = new TestDateTimeProvider();
        var handlerA = new AssignVehicleCommandHandler(repoA, uowA, currentUserA, dtProviderA);

        var repoB = new EfVehicleRepository(contextB);
        var uowB = new EfUnitOfWork(contextB);
        var currentUserB = new TestCurrentUser(dispatcher.Id);
        var dtProviderB = new TestDateTimeProvider();
        var handlerB = new AssignVehicleCommandHandler(repoB, uowB, currentUserB, dtProviderB);

        var commandA = new AssignVehicleCommand(vehicle.Id, driver1.Id);
        var commandB = new AssignVehicleCommand(vehicle.Id, driver2.Id);

        // Act: 真实并发触发两个 Handler
        var taskA = Task.Run(() => handlerA.Handle(commandA, CancellationToken.None));
        var taskB = Task.Run(() => handlerB.Handle(commandB, CancellationToken.None));

        var results = await Task.WhenAll(taskA, taskB);
        var resultA = results[0];
        var resultB = results[1];

        // Assert: 恰好一个成功，另一个返回 409 Conflict
        var successCount = (resultA.IsSuccess ? 1 : 0) + (resultB.IsSuccess ? 1 : 0);
        var conflictCount = (!resultA.IsSuccess && resultA.Error?.Kind == ErrorKind.Conflict ? 1 : 0) +
                            (!resultB.IsSuccess && resultB.Error?.Kind == ErrorKind.Conflict ? 1 : 0);

        successCount.Should().Be(1, "Exactly one concurrent assignment must succeed");
        conflictCount.Should().Be(1, "The competing assignment must fail with 409 Conflict");

        var failedResult = !resultA.IsSuccess ? resultA : resultB;
        failedResult.Error!.Code.Should().Be("vehicle_already_assigned");

        // 验证数据库最终状态：该车在数据库中恰好只有 1 条活跃分派
        await using var verifyContext = _fixture.CreateDbContext();
        var activeAssignments = await verifyContext.VehicleAssignments
            .Where(a => a.VehicleId == vehicle.Id && a.ReleasedAt == null)
            .ToListAsync();

        activeAssignments.Should().ContainSingle();
    }

    [Fact]
    public async Task F3_2_ReleaseAssignment_AllowsSubsequentAssignmentForSameVehicle()
    {
        // Arrange
        var (dispatcher, driver1, driver2) = await SeedPrerequisitesAsync();

        await using var context = _fixture.CreateDbContext();
        var repo = new EfVehicleRepository(context);
        var uow = new EfUnitOfWork(context);
        var currentUser = new TestCurrentUser(dispatcher.Id);
        var dtProvider = new TestDateTimeProvider();

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("R"),
            "Hino",
            "500",
            2022,
            "ENC(VIN)",
            new Kilometres(40000),
            new Kilometres(15000));
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        var assignHandler = new AssignVehicleCommandHandler(repo, uow, currentUser, dtProvider);
        var releaseHandler = new ReleaseVehicleAssignmentCommandHandler(repo, uow, dtProvider);

        // 第一次分派给 Driver 1
        var assignResult1 = await assignHandler.Handle(new AssignVehicleCommand(vehicle.Id, driver1.Id), CancellationToken.None);
        assignResult1.IsSuccess.Should().BeTrue();

        // 释放第一次分派
        var releaseResult = await releaseHandler.Handle(new ReleaseVehicleAssignmentCommand(assignResult1.Value), CancellationToken.None);
        releaseResult.IsSuccess.Should().BeTrue();

        // 释放后再分派给 Driver 2，应当成功
        var assignResult2 = await assignHandler.Handle(new AssignVehicleCommand(vehicle.Id, driver2.Id), CancellationToken.None);
        assignResult2.IsSuccess.Should().BeTrue();
        assignResult2.Value.Should().NotBe(assignResult1.Value);

        // 验证历史记录：该车有 2 条记录，1 条已释放，1 条活跃
        var history = await repo.GetAssignmentsByVehicleIdAsync(vehicle.Id);
        history.Should().HaveCount(2);
        history.Count(h => h.IsActive).Should().Be(1);
    }

    #endregion

    #region F3.3 里程上报测试

    [Fact]
    public async Task F3_3_RecordOdometerReading_ValidReading_UpdatesVehicleOdometerAndPersists()
    {
        // Arrange
        var (_, driver1, _) = await SeedPrerequisitesAsync();

        await using var context = _fixture.CreateDbContext();
        var repo = new EfVehicleRepository(context);
        var uow = new EfUnitOfWork(context);
        var dtProvider = new TestDateTimeProvider();
        var currentUser = new TestCurrentUser(driver1.Id, UserRole.Dispatcher);
        var handler = new RecordOdometerReadingCommandHandler(repo, uow, currentUser, dtProvider);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("O"),
            "Toyota",
            "Hilux",
            2023,
            "ENC(VIN)",
            new Kilometres(10000),
            new Kilometres(10000));
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        var baseNow = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var command = new RecordOdometerReadingCommand(
            vehicle.Id,
            driver1.Id,
            12500m,
            "media/odometer/photo_123.jpg",
            baseNow,
            "DriverApp");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verifyContext = _fixture.CreateDbContext();
        var updatedVehicle = await verifyContext.Vehicles.FindAsync(vehicle.Id);
        updatedVehicle!.OdometerKm.Value.Should().Be(12500m);

        var reading = await verifyContext.OdometerReadings.FindAsync(result.Value);
        reading.Should().NotBeNull();
        reading!.ReadingKm.Value.Should().Be(12500m);
        reading.PhotoKey.Should().Be("media/odometer/photo_123.jpg");
    }

    [Fact]
    public async Task F3_3_RecordOdometerReading_DecreasingReading_Returns422Unprocessable()
    {
        // Arrange
        var (_, driver1, _) = await SeedPrerequisitesAsync();

        await using var context = _fixture.CreateDbContext();
        var repo = new EfVehicleRepository(context);
        var uow = new EfUnitOfWork(context);
        var dtProvider = new TestDateTimeProvider();
        var currentUser = new TestCurrentUser(driver1.Id, UserRole.Dispatcher);
        var handler = new RecordOdometerReadingCommandHandler(repo, uow, currentUser, dtProvider);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("D"),
            "Toyota",
            "Hilux",
            2023,
            "ENC(VIN)",
            new Kilometres(20000),
            new Kilometres(10000));
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        var command = new RecordOdometerReadingCommand(
            vehicle.Id,
            driver1.Id,
            18500m); // Less than 20000

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("odometer_reading_cannot_decrease");

        // 验证车辆里程未被篡改
        await using var verifyContext = _fixture.CreateDbContext();
        var checkVehicle = await verifyContext.Vehicles.FindAsync(vehicle.Id);
        checkVehicle!.OdometerKm.Value.Should().Be(20000m);
    }

    #endregion

    #region Query Projections 测试

    [Fact]
    public async Task QueryProjections_ReturnExpectedData()
    {
        // Arrange
        var (dispatcher, driver1, _) = await SeedPrerequisitesAsync();

        await using var context = _fixture.CreateDbContext();
        var repo = new EfVehicleRepository(context);

        var rego = TestDataFactory.CreateRegoObject("P");
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            rego,
            "Fuso",
            "Canter",
            2022,
            "ENC(VIN)",
            new Kilometres(15000),
            new Kilometres(10000),
            new Kilometres(5000),
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 1),
            VehicleStatus.Active);
        await context.Vehicles.AddAsync(vehicle);

        var baseNow = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var assignment = new VehicleAssignment(
            Guid.NewGuid(),
            vehicle.Id,
            driver1.Id,
            baseNow,
            dispatcher.Id);
        await context.VehicleAssignments.AddAsync(assignment);

        var reading = new OdometerReading(
            Guid.NewGuid(),
            vehicle.Id,
            driver1.Id,
            new Kilometres(15000),
            "photo.jpg",
            baseNow,
            "DriverApp");
        await context.OdometerReadings.AddAsync(reading);
        await context.SaveChangesAsync();

        // Act & Assert Detail
        var detailHandler = new GetVehicleByIdQueryHandler(repo);
        var detailResult = await detailHandler.Handle(new GetVehicleByIdQuery(vehicle.Id), CancellationToken.None);
        detailResult.IsSuccess.Should().BeTrue();
        detailResult.Value.Rego.Should().Be(rego.Value);
        detailResult.Value.ActiveAssignment.Should().NotBeNull();
        detailResult.Value.ActiveAssignment!.DriverId.Should().Be(driver1.Id);
        detailResult.Value.LatestOdometerReading.Should().NotBeNull();
        detailResult.Value.LatestOdometerReading!.ReadingKm.Should().Be(15000m);

        // Act & Assert List
        var listHandler = new GetVehiclesListQueryHandler(repo);
        var listResult = await listHandler.Handle(new GetVehiclesListQuery(Search: rego.Value), CancellationToken.None);
        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Items.Should().ContainSingle(v => v.Rego == rego.Value);

        // Act & Assert Active Assignment
        var activeHandler = new GetActiveVehicleAssignmentQueryHandler(repo);
        var activeResult = await activeHandler.Handle(new GetActiveVehicleAssignmentQuery(vehicle.Id), CancellationToken.None);
        activeResult.IsSuccess.Should().BeTrue();
        activeResult.Value.Should().NotBeNull();
        activeResult.Value!.DriverId.Should().Be(driver1.Id);

        // Act & Assert Odometer Readings
        var odoHandler = new GetOdometerReadingsQueryHandler(repo);
        var odoResult = await odoHandler.Handle(new GetOdometerReadingsQuery(vehicle.Id), CancellationToken.None);
        odoResult.IsSuccess.Should().BeTrue();
        odoResult.Value.Should().ContainSingle(r => r.ReadingKm == 15000m);
    }

    #endregion

    private sealed class TestCurrentUser(Guid userId, UserRole role = UserRole.Dispatcher) : ICurrentUser
    {
        public Guid? UserId => userId;
        public UserRole? Role => role;
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "IntegrationTest";
        public bool IsAuthenticated => true;
    }

    private sealed class TestDateTimeProvider(DateTimeOffset? fixedUtcNow = null) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = fixedUtcNow ?? new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset NzNow => UtcNow.ToOffset(TimeSpan.FromHours(12));
        public DateOnly NzToday => DateOnly.FromDateTime(NzNow.DateTime);
    }
}
