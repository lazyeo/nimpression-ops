using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Commands.AcknowledgeJobTask;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Idempotency;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Repositories;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Dispatch;

[Collection("PostgreSqlCollection")]
public class IdempotencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public IdempotencyIntegrationTests(PostgreSqlContainerFixture fixture)
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

    private async Task<(User User, Driver Driver, Vehicle Vehicle, Area Area)> SeedBaseEntitiesAsync()
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
            "Central Zone",
            GenerateRef("AKL-IDEMP"),
            "Idempotency Test Area");

        await context.Users.AddAsync(user);
        await context.Drivers.AddAsync(driver);
        await context.Vehicles.AddAsync(vehicle);
        await context.Areas.AddAsync(area);
        await context.SaveChangesAsync();

        return (user, driver, vehicle, area);
    }

    #region F5.4 离线幂等重放与并发测试

    /// <summary>
    /// F5.4 关键验收测试：两个并发请求携带同一 ClientRequestId，只有一个真正执行业务逻辑，另一个返回缓存响应。
    /// 使用两个独立 DbContext 配合 Task.WhenAll 构造真实并发。
    /// </summary>
    [Fact]
    public async Task F5_4_ConcurrentRequests_WithSameClientRequestId_OnlyOneExecutes()
    {
        // Arrange
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync();

        await using var seedContext = _fixture.CreateDbContext();
        var dtProvider = new TestDateTimeProvider();
        var driverUser = new TestCurrentUser(user.Id, UserRole.Driver);

        // 创建已指派给该司机的任务
        var task = new JobTask(
            Guid.NewGuid(),
            GenerateRef("TSK-CONCUR"),
            "Concurrent Idempotency Test Task",
            area.Id,
            dtProvider.UtcNow,
            user.Id);
        task.Assign(driver.Id, vehicle.Id, null, dtProvider.UtcNow);
        await seedContext.JobTasks.AddAsync(task);
        await seedContext.SaveChangesAsync();

        var clientRequestId = Guid.NewGuid().ToString();
        var payload = new { TaskId = task.Id, Action = "Acknowledge" };
        var command = new AcknowledgeJobTaskCommand(task.Id);

        var executionCount = 0;

        // 构造两个独立的 DbContext 与 IdempotencyService 实例
        await using var context1 = _fixture.CreateDbContext();
        await using var context2 = _fixture.CreateDbContext();

        var idempotencyService1 = new IdempotencyService(context1, dtProvider);
        var idempotencyService2 = new IdempotencyService(context2, dtProvider);

        async Task<Result> ExecuteRequestAsync(AppDbContext ctx, IdempotencyService svc)
        {
            var repo = new JobTaskRepository(ctx);
            var uow = new UnitOfWork(ctx);
            var handler = new AcknowledgeJobTaskCommandHandler(repo, uow, driverUser, dtProvider);

            return await svc.ExecuteAsync(
                clientRequestId,
                payload,
                async () =>
                {
                    Interlocked.Increment(ref executionCount);
                    return await handler.Handle(command, CancellationToken.None);
                });
        }

        // Act: 通过 Task.WhenAll 并发提交两个请求
        var results = await Task.WhenAll(
            ExecuteRequestAsync(context1, idempotencyService1),
            ExecuteRequestAsync(context2, idempotencyService2));

        // Assert:
        // 1. 两个请求对外均返回成功
        results[0].IsSuccess.Should().BeTrue();
        results[1].IsSuccess.Should().BeTrue();

        // 2. 真实业务逻辑仅被执行了一次（幂等生效）
        executionCount.Should().Be(1);

        // 3. 数据库内任务状态已成功流转为 Acknowledged
        await using var verifyContext = _fixture.CreateDbContext();
        var finalTask = await verifyContext.JobTasks.FindAsync(task.Id);
        finalTask.Should().NotBeNull();
        finalTask!.Status.Should().Be(JobTaskStatus.Acknowledged);
        finalTask.AcknowledgedAt.Should().NotBeNull();

        // 4. 幂等表中存在且仅存在一条该 Key 的记录
        var idempotencyRecord = await verifyContext.IdempotencyRecords.FindAsync(clientRequestId);
        idempotencyRecord.Should().NotBeNull();
        idempotencyRecord!.StatusCode.Should().Be(200);
    }

    /// <summary>
    /// F5.4: 同 Key + 不同请求内容 -> 返回 409 Conflict（客户端 Bug，不能静默覆盖）。
    /// </summary>
    [Fact]
    public async Task F5_4_SameClientRequestId_WithDifferentPayload_Returns409Conflict()
    {
        // Arrange
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync();

        await using var seedContext = _fixture.CreateDbContext();
        var dtProvider = new TestDateTimeProvider();
        var driverUser = new TestCurrentUser(user.Id, UserRole.Driver);

        var task = new JobTask(
            Guid.NewGuid(),
            GenerateRef("TSK-DIFF-PAYLOAD"),
            "Different Payload Test",
            area.Id,
            dtProvider.UtcNow,
            user.Id);
        task.Assign(driver.Id, vehicle.Id, null, dtProvider.UtcNow);
        await seedContext.JobTasks.AddAsync(task);
        await seedContext.SaveChangesAsync();

        var clientRequestId = Guid.NewGuid().ToString();

        // 第一次请求：Payload A
        await using var context1 = _fixture.CreateDbContext();
        var svc1 = new IdempotencyService(context1, dtProvider);
        var repo1 = new JobTaskRepository(context1);
        var uow1 = new UnitOfWork(context1);
        var handler1 = new AcknowledgeJobTaskCommandHandler(repo1, uow1, driverUser, dtProvider);

        var result1 = await svc1.ExecuteAsync(
            clientRequestId,
            new { TaskId = task.Id, PayloadVersion = 1 },
            () => handler1.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None));

        result1.IsSuccess.Should().BeTrue();

        // 第二次请求：同 Key，但 Payload 变为 Payload B
        await using var context2 = _fixture.CreateDbContext();
        var svc2 = new IdempotencyService(context2, dtProvider);
        var repo2 = new JobTaskRepository(context2);
        var uow2 = new UnitOfWork(context2);
        var handler2 = new AcknowledgeJobTaskCommandHandler(repo2, uow2, driverUser, dtProvider);

        var result2 = await svc2.ExecuteAsync(
            clientRequestId,
            new { TaskId = task.Id, PayloadVersion = 2 }, // 不同的载荷内容
            () => handler2.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None));

        // Assert: 返回 409 Conflict
        result2.IsSuccess.Should().BeFalse();
        result2.Error.Should().NotBeNull();
        result2.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result2.Error.Code.Should().Be("idempotency_key_mismatch");
    }

    /// <summary>
    /// F5.4: 串行重试（离线队列重放）：二次提交返回首次结果，不执行重复副作用。
    /// </summary>
    [Fact]
    public async Task F5_4_SequentialReplay_ReturnsCachedResponseWithoutSideEffects()
    {
        // Arrange
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync();

        await using var seedContext = _fixture.CreateDbContext();
        var dtProvider = new TestDateTimeProvider();
        var driverUser = new TestCurrentUser(user.Id, UserRole.Driver);

        var task = new JobTask(
            Guid.NewGuid(),
            GenerateRef("TSK-SEQ-REPLAY"),
            "Sequential Replay Task",
            area.Id,
            dtProvider.UtcNow,
            user.Id);
        task.Assign(driver.Id, vehicle.Id, null, dtProvider.UtcNow);
        await seedContext.JobTasks.AddAsync(task);
        await seedContext.SaveChangesAsync();

        var clientRequestId = Guid.NewGuid().ToString();
        var payload = new { TaskId = task.Id, Action = "Acknowledge" };
        var executionCount = 0;

        // 第一次执行
        await using var context1 = _fixture.CreateDbContext();
        var svc1 = new IdempotencyService(context1, dtProvider);
        var repo1 = new JobTaskRepository(context1);
        var uow1 = new UnitOfWork(context1);
        var handler1 = new AcknowledgeJobTaskCommandHandler(repo1, uow1, driverUser, dtProvider);

        var result1 = await svc1.ExecuteAsync(
            clientRequestId,
            payload,
            async () =>
            {
                Interlocked.Increment(ref executionCount);
                return await handler1.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None);
            });

        result1.IsSuccess.Should().BeTrue();
        executionCount.Should().Be(1);

        // 第二次重放
        await using var context2 = _fixture.CreateDbContext();
        var svc2 = new IdempotencyService(context2, dtProvider);
        var repo2 = new JobTaskRepository(context2);
        var uow2 = new UnitOfWork(context2);
        var handler2 = new AcknowledgeJobTaskCommandHandler(repo2, uow2, driverUser, dtProvider);

        var result2 = await svc2.ExecuteAsync(
            clientRequestId,
            payload,
            async () =>
            {
                Interlocked.Increment(ref executionCount);
                return await handler2.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None);
            });

        // Assert: 二次重放依然返回成功，但业务 action 没有被再次执行
        result2.IsSuccess.Should().BeTrue();
        executionCount.Should().Be(1);
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
}
