using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Commands.AcknowledgeJobTask;
using Nimpression.Application.Features.Dispatch.Commands.AssignJobTask;
using Nimpression.Application.Features.Dispatch.Commands.CancelJobTask;
using Nimpression.Application.Features.Dispatch.Commands.CompleteJobTask;
using Nimpression.Application.Features.Dispatch.Commands.CreateJobTask;
using Nimpression.Application.Features.Dispatch.Commands.StartJobTask;
using Nimpression.Application.Features.Dispatch.DTOs;
using Nimpression.Application.Features.Dispatch.Queries.CheckAreaEligibility;
using Nimpression.Application.Features.Dispatch.Queries.GetJobTaskById;
using Nimpression.Application.Features.Dispatch.Queries.GetJobTasksList;
using Nimpression.Application.Features.Dispatch.Queries.GetUnacknowledgedTaskAlerts;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Repositories;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Dispatch;

[Collection("PostgreSqlCollection")]
public class DispatchIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public DispatchIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string GenerateRef(string prefix) =>
        $"{prefix}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..6].ToUpperInvariant()}";

    private async Task<(User User, Driver Driver, Vehicle Vehicle, Area Area)> SeedBaseEntitiesAsync(bool assignDriverToArea = true)
    {
        await using var context = _fixture.CreateDbContext();

        var user = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver"),
            "hash",
            UserRole.Driver,
            "Integration Driver",
            "en-NZ",
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        var driver = new Driver(
            Guid.NewGuid(),
            user.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(32m),
            new Money(45m),
            new Money(0.85m),
            "ENC(021123456)",
            "ENC(123 Main Rd)",
            "ENC(Emergency Contact)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego(TestDataFactory.CreateRego()),
            "Toyota",
            "Hilux",
            2022,
            "ENC(7AT00000000000000)",
            new Kilometres(120000m),
            new Kilometres(10000m),
            new Kilometres(115000m),
            new DateOnly(2028, 1, 1),
            null,
            new DateOnly(2028, 1, 1),
            VehicleStatus.Active);

        var area = new Area(
            Guid.NewGuid(),
            "Central Dispatch Zone",
            GenerateRef("AKL-ZONE"),
            "CBD Delivery Zone");

        await context.Users.AddAsync(user);
        await context.Drivers.AddAsync(driver);
        await context.Vehicles.AddAsync(vehicle);
        await context.Areas.AddAsync(area);

        if (assignDriverToArea)
        {
            var assignment = new AreaAssignment(
                Guid.NewGuid(),
                area.Id,
                driver.Id,
                new DateOnly(2026, 1, 1),
                new DateOnly(2028, 12, 31));
            await context.AreaAssignments.AddAsync(assignment);
        }

        await context.SaveChangesAsync();
        return (user, driver, vehicle, area);
    }

    #region F5.1 & F4.3 任务创建与派单约束

    [Fact]
    public async Task F5_1_CreateJobTask_WithDriverAndVehicle_PersistsSuccessfully()
    {
        // Arrange
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync(assignDriverToArea: true);

        await using var context = _fixture.CreateDbContext();
        var repo = new JobTaskRepository(context);
        var uow = new UnitOfWork(context);
        var currentUser = new TestCurrentUser(user.Id, UserRole.Dispatcher);
        var auditSink = new TestAuditSink();

        var handler = new CreateJobTaskCommandHandler(repo, uow, currentUser, auditSink);
        var scheduledFor = new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(12));

        var command = new CreateJobTaskCommand(
            GenerateRef("TSK-CREATE"),
            "Morning Distribution Run",
            area.Id,
            scheduledFor,
            TaskPriority.High,
            "Deliver bulk freight",
            65m,
            driver.Id,
            vehicle.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        await using var verifyContext = _fixture.CreateDbContext();
        var saved = await verifyContext.JobTasks.FindAsync(result.Value);
        saved.Should().NotBeNull();
        saved!.Title.Should().Be("Morning Distribution Run");
        saved.Status.Should().Be(JobTaskStatus.Assigned);
        saved.DriverId.Should().Be(driver.Id);
        saved.VehicleId.Should().Be(vehicle.Id);
        saved.AreaId.Should().Be(area.Id);
    }

    [Fact]
    public async Task F4_3_CreateJobTask_AreaMismatch_WithoutOverride_Returns422Warning()
    {
        // Arrange: 司机未分配到该区域
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync(assignDriverToArea: false);

        await using var context = _fixture.CreateDbContext();
        var repo = new JobTaskRepository(context);
        var uow = new UnitOfWork(context);
        var currentUser = new TestCurrentUser(user.Id, UserRole.Dispatcher);
        var auditSink = new TestAuditSink();

        var handler = new CreateJobTaskCommandHandler(repo, uow, currentUser, auditSink);
        var scheduledFor = new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(12));

        var command = new CreateJobTaskCommand(
            GenerateRef("TSK-WARN"),
            "Out of area run",
            area.Id,
            scheduledFor,
            DriverId: driver.Id,
            VehicleId: vehicle.Id,
            OverrideAreaWarning: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 422 警告提示
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("area_mismatch_warning");
    }

    [Fact]
    public async Task F4_3_CreateJobTask_AreaMismatch_WithOverride_SucceedsAndAudits()
    {
        // Arrange: 司机未分配到该区域，但请求指定 OverrideAreaWarning = true
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync(assignDriverToArea: false);

        await using var context = _fixture.CreateDbContext();
        var repo = new JobTaskRepository(context);
        var uow = new UnitOfWork(context);
        var currentUser = new TestCurrentUser(user.Id, UserRole.Dispatcher);
        var auditSink = new TestAuditSink();

        var handler = new CreateJobTaskCommandHandler(repo, uow, currentUser, auditSink);
        var scheduledFor = new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(12));

        var command = new CreateJobTaskCommand(
            GenerateRef("TSK-OVR"),
            "Cross area override run",
            area.Id,
            scheduledFor,
            DriverId: driver.Id,
            VehicleId: vehicle.Id,
            OverrideAreaWarning: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 越过成功并写审计
        result.IsSuccess.Should().BeTrue();

        auditSink.Audits.Should().ContainSingle(a => a.Action == "OverrideAreaWarning");
    }

    #endregion

    #region F5.2 & F5.3 司机任务状态机跃迁与非法跃迁约束

    [Fact]
    public async Task F5_2_DriverLifecycle_FullTransition_Succeeds()
    {
        // Arrange
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync(assignDriverToArea: true);

        await using var context = _fixture.CreateDbContext();
        var repo = new JobTaskRepository(context);
        var uow = new UnitOfWork(context);
        var dtProvider = new TestDateTimeProvider();
        var driverUser = new TestCurrentUser(user.Id, UserRole.Driver);

        // 1. 创建草稿任务
        var task = new JobTask(
            Guid.NewGuid(),
            GenerateRef("TSK-FLOW"),
            "Lifecycle Test Task",
            area.Id,
            dtProvider.UtcNow,
            user.Id);
        await context.JobTasks.AddAsync(task);
        await context.SaveChangesAsync();

        // 2. 指派任务 (Draft -> Assigned)
        var assignHandler = new AssignJobTaskCommandHandler(repo, uow, dtProvider, new TestAuditSink());
        var assignRes = await assignHandler.Handle(new AssignJobTaskCommand(task.Id, driver.Id, vehicle.Id), CancellationToken.None);
        assignRes.IsSuccess.Should().BeTrue();

        // 3. 司机确认任务 (Assigned -> Acknowledged)
        var ackHandler = new AcknowledgeJobTaskCommandHandler(repo, uow, driverUser, dtProvider);
        var ackRes = await ackHandler.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None);
        ackRes.IsSuccess.Should().BeTrue();

        // 4. 司机开始任务 (Acknowledged -> InProgress)
        var startHandler = new StartJobTaskCommandHandler(repo, uow, driverUser, dtProvider);
        var startRes = await startHandler.Handle(new StartJobTaskCommand(task.Id, StartOdometerKm: 120000m), CancellationToken.None);
        startRes.IsSuccess.Should().BeTrue();

        // 5. 司机完成任务 (InProgress -> Completed)
        var completeHandler = new CompleteJobTaskCommandHandler(repo, uow, driverUser, dtProvider);
        var compRes = await completeHandler.Handle(new CompleteJobTaskCommand(task.Id, ActualDistanceKm: 35m, EndOdometerKm: 120035m), CancellationToken.None);
        compRes.IsSuccess.Should().BeTrue();

        // 验证数据库最终状态
        await using var verifyContext = _fixture.CreateDbContext();
        var finalTask = await verifyContext.JobTasks.FindAsync(task.Id);
        finalTask.Should().NotBeNull();
        finalTask!.Status.Should().Be(JobTaskStatus.Completed);
        finalTask.EffectiveDistanceKm?.Value.Should().Be(35m);
        finalTask.AcknowledgedAt.Should().NotBeNull();
        finalTask.StartedAt.Should().NotBeNull();
        finalTask.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task F5_3_InvalidJobTaskTransition_Returns422Unprocessable()
    {
        // Arrange: 任务在 Draft 状态，直接调用 Acknowledge
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync(assignDriverToArea: true);

        await using var context = _fixture.CreateDbContext();
        var repo = new JobTaskRepository(context);
        var uow = new UnitOfWork(context);
        var dtProvider = new TestDateTimeProvider();
        var dispatcherUser = new TestCurrentUser(user.Id, UserRole.Dispatcher);

        var task = new JobTask(
            Guid.NewGuid(),
            GenerateRef("TSK-INV"),
            "Invalid Transition Task",
            area.Id,
            dtProvider.UtcNow,
            user.Id);
        await context.JobTasks.AddAsync(task);
        await context.SaveChangesAsync();

        var ackHandler = new AcknowledgeJobTaskCommandHandler(repo, uow, dispatcherUser, dtProvider);

        // Act: Draft -> Acknowledged (Dispatcher tries to acknowledge a Draft task -> 422 InvalidJobTaskTransition)
        var result = await ackHandler.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None);

        // Assert: 返回 422
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("invalid_task_transition");
    }

    #endregion

    #region F5.5 未确认提醒查询

    [Fact]
    public async Task F5_5_GetUnacknowledgedAlerts_QueriesAssignedTasksOverThreshold()
    {
        // Arrange
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync(assignDriverToArea: true);

        await using var context = _fixture.CreateDbContext();
        var dtProvider = new TestDateTimeProvider();

        // 任务 A: 指派后 45 分钟前（超过 30 分钟阈值）且仍处于 Assigned
        var taskA = new JobTask(
            Guid.NewGuid(),
            GenerateRef("TSK-ALERT-A"),
            "Alert Task A",
            area.Id,
            dtProvider.UtcNow.AddMinutes(-45),
            user.Id);
        taskA.Assign(driver.Id, vehicle.Id, null, dtProvider.UtcNow.AddMinutes(-45));
        await context.JobTasks.AddAsync(taskA);

        // 任务 B: 指派后 10 分钟前（未超过 30 分钟）
        var taskB = new JobTask(
            Guid.NewGuid(),
            GenerateRef("TSK-ALERT-B"),
            "Alert Task B",
            area.Id,
            dtProvider.UtcNow.AddMinutes(-10),
            user.Id);
        taskB.Assign(driver.Id, vehicle.Id, null, dtProvider.UtcNow.AddMinutes(-10));
        await context.JobTasks.AddAsync(taskB);

        // 任务 C: 指派后 60 分钟前，但司机已确认 (Acknowledged)
        var taskC = new JobTask(
            Guid.NewGuid(),
            GenerateRef("TSK-ALERT-C"),
            "Alert Task C",
            area.Id,
            dtProvider.UtcNow.AddMinutes(-60),
            user.Id);
        taskC.Assign(driver.Id, vehicle.Id, null, dtProvider.UtcNow.AddMinutes(-60));
        taskC.Acknowledge(dtProvider.UtcNow.AddMinutes(-50));
        await context.JobTasks.AddAsync(taskC);

        await context.SaveChangesAsync();

        var repo = new JobTaskRepository(context);
        var queryHandler = new GetUnacknowledgedTaskAlertsQueryHandler(repo, dtProvider);

        // Act
        var result = await queryHandler.Handle(new GetUnacknowledgedTaskAlertsQuery(ThresholdMinutes: 30), CancellationToken.None);

        // Assert: 只有 taskA 产出提醒
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(x => x.TaskId == taskA.Id);
        result.Value.Should().NotContain(x => x.TaskId == taskB.Id);
        result.Value.Should().NotContain(x => x.TaskId == taskC.Id);
    }

    #endregion

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset NzNow => UtcNow.ToOffset(TimeSpan.FromHours(12));
        public DateOnly NzToday => DateOnly.FromDateTime(NzNow.DateTime);
    }

    private sealed class TestCurrentUser(Guid userId, UserRole role) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public UserRole? Role { get; } = role;
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "TestAgent";
        public bool IsAuthenticated => true;
    }

    private sealed class TestAuditSink : IAuditSink
    {
        public List<(string EntityType, Guid? EntityId, string Action)> Audits { get; } = [];

        public Task RecordAsync(string entityType, Guid? entityId, string action, string? beforeJson = null, string? afterJson = null, CancellationToken cancellationToken = default)
        {
            Audits.Add((entityType, entityId, action));
            return Task.CompletedTask;
        }
    }
}
