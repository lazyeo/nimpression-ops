using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Api.Endpoints;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Commands.AcknowledgeJobTask;
using Nimpression.Application.Features.Identity.DTOs;
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
using Nimpression.Infrastructure.Security;
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

        var uow1 = new UnitOfWork(context1);
        var uow2 = new UnitOfWork(context2);

        var idempotencyService1 = new IdempotencyService(context1, uow1, dtProvider);
        var idempotencyService2 = new IdempotencyService(context2, uow2, dtProvider);

        async Task<Result> ExecuteRequestAsync(AppDbContext ctx, IUnitOfWork uow, IdempotencyService svc)
        {
            var repo = new JobTaskRepository(ctx);
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
            ExecuteRequestAsync(context1, uow1, idempotencyService1),
            ExecuteRequestAsync(context2, uow2, idempotencyService2));

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
        var uow1 = new UnitOfWork(context1);
        var svc1 = new IdempotencyService(context1, uow1, dtProvider);
        var repo1 = new JobTaskRepository(context1);
        var handler1 = new AcknowledgeJobTaskCommandHandler(repo1, uow1, driverUser, dtProvider);

        var result1 = await svc1.ExecuteAsync(
            clientRequestId,
            new { TaskId = task.Id, PayloadVersion = 1 },
            () => handler1.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None));

        result1.IsSuccess.Should().BeTrue();

        // 第二次请求：同 Key，但 Payload 变为 Payload B
        await using var context2 = _fixture.CreateDbContext();
        var uow2 = new UnitOfWork(context2);
        var svc2 = new IdempotencyService(context2, uow2, dtProvider);
        var repo2 = new JobTaskRepository(context2);
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
        var uow1 = new UnitOfWork(context1);
        var svc1 = new IdempotencyService(context1, uow1, dtProvider);
        var repo1 = new JobTaskRepository(context1);
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
        var uow2 = new UnitOfWork(context2);
        var svc2 = new IdempotencyService(context2, uow2, dtProvider);
        var repo2 = new JobTaskRepository(context2);
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

    #region HTTP End-to-End Idempotency & Transaction Regression Tests (W17)

    /// <summary>
    /// W17 / R2: 通过 HTTP 创建任务成功返回 201 Created（而非 500 This NpgsqlTransaction has completed）。
    /// 验证任务与幂等记录在同一事务内原子落库。
    /// </summary>
    [Fact]
    public async Task W17_Http_CreateJobTask_WithClientRequestId_Returns201Created_AndPersistsTaskAndIdempotencyRecord()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync();

        var dispatcherId = Guid.NewGuid();
        var dispatcherEmail = TestDataFactory.CreateEmail("disp_w17");
        // allow-hardcoded: 测试夹具专用的假口令，不涉及任何真实凭据
        var password = "SecurePassword123!";
        var hasher = new PasswordHasher();
        var dispatcher = new User(dispatcherId, new EmailAddress(dispatcherEmail), hasher.HashPassword(password), UserRole.Dispatcher, "Dispatcher W17");
        await context.Users.AddAsync(dispatcher);
        await context.SaveChangesAsync();

        using var factory = new DispatchTestWebApplicationFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(dispatcherEmail, password));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var authBody = await loginResp.Content.ReadFromJsonAsync<AuthSuccessResponse>();
        var token = authBody!.AccessToken;

        var clientRequestId = Guid.NewGuid().ToString();
        var requestPayload = new CreateJobTaskRequest(
            Ref: GenerateRef("TSK-W17-1"),
            Title: "W17 Idempotency Test Task",
            AreaId: area.Id,
            ScheduledFor: new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(12)),
            Priority: TaskPriority.High,
            Description: "Testing W17 HTTP creation with idempotency key",
            PlannedDistanceKm: 42m,
            DriverId: driver.Id,
            VehicleId: vehicle.Id,
            OverrideAreaWarning: true);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/dispatch/tasks")
        {
            Content = JsonContent.Create(requestPayload)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Headers.Add("X-Client-Request-Id", clientRequestId);

        // Act
        var response = await client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // 验证数据库中存在且仅存在 1 条任务和 1 条幂等记录
        await using var verifyContext = _fixture.CreateDbContext();
        var savedTask = await verifyContext.JobTasks.FirstOrDefaultAsync(t => t.Ref == requestPayload.Ref);
        savedTask.Should().NotBeNull();

        var idempotencyRecord = await verifyContext.IdempotencyRecords.FindAsync(clientRequestId);
        idempotencyRecord.Should().NotBeNull();
        idempotencyRecord!.StatusCode.Should().Be(200);
    }

    /// <summary>
    /// W17 / R2: 同一幂等键重复请求返回相同结果且不产生第二条业务记录。
    /// </summary>
    [Fact]
    public async Task W17_Http_CreateJobTask_DuplicateClientRequestId_ReturnsSameResult_AndDoesNotDuplicateBusinessRecord()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync();

        var dispatcherId = Guid.NewGuid();
        var dispatcherEmail = TestDataFactory.CreateEmail("disp_w17_dup");
        // allow-hardcoded: 测试夹具专用的假口令，不涉及任何真实凭据
        var password = "SecurePassword123!";
        var hasher = new PasswordHasher();
        var dispatcher = new User(dispatcherId, new EmailAddress(dispatcherEmail), hasher.HashPassword(password), UserRole.Dispatcher, "Dispatcher W17 Dup");
        await context.Users.AddAsync(dispatcher);
        await context.SaveChangesAsync();

        using var factory = new DispatchTestWebApplicationFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(dispatcherEmail, password));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var authBody = await loginResp.Content.ReadFromJsonAsync<AuthSuccessResponse>();
        var token = authBody!.AccessToken;

        var clientRequestId = Guid.NewGuid().ToString();
        var requestPayload = new CreateJobTaskRequest(
            Ref: GenerateRef("TSK-W17-DUP"),
            Title: "W17 Duplicate Test Task",
            AreaId: area.Id,
            ScheduledFor: new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(12)),
            Priority: TaskPriority.Medium,
            Description: "Testing duplicate idempotent submission",
            PlannedDistanceKm: 30m,
            DriverId: driver.Id,
            VehicleId: vehicle.Id,
            OverrideAreaWarning: true);

        // 第一次请求
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/dispatch/tasks")
        {
            Content = JsonContent.Create(requestPayload)
        };
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req1.Headers.Add("X-Client-Request-Id", clientRequestId);

        var resp1 = await client.SendAsync(req1);
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);
        var respContent1 = await resp1.Content.ReadAsStringAsync();

        // 第二次请求（同一幂等键 + 相同内容）
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/dispatch/tasks")
        {
            Content = JsonContent.Create(requestPayload)
        };
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req2.Headers.Add("X-Client-Request-Id", clientRequestId);

        var resp2 = await client.SendAsync(req2);

        // Assert: 状态码一致，返回首次结果
        resp2.StatusCode.Should().Be(HttpStatusCode.Created);
        var respContent2 = await resp2.Content.ReadAsStringAsync();
        respContent2.Should().Be(respContent1);

        // 数据库中不产生第二条业务记录
        await using var verifyContext = _fixture.CreateDbContext();
        var taskCount = await verifyContext.JobTasks.CountAsync(t => t.Ref == requestPayload.Ref);
        taskCount.Should().Be(1);
    }

    /// <summary>
    /// W17 / R2: 业务失败时幂等记录不残留（否则重试会被误判为已处理）。
    /// </summary>
    [Fact]
    public async Task W17_Http_CreateJobTask_BusinessFailure_DoesNotLeaveIdempotencyRecord_AndAllowsSubsequentRetry()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var (user, driver, vehicle, area) = await SeedBaseEntitiesAsync();

        var dispatcherId = Guid.NewGuid();
        var dispatcherEmail = TestDataFactory.CreateEmail("disp_w17_fail");
        // allow-hardcoded: 测试夹具专用的假口令，不涉及任何真实凭据
        var password = "SecurePassword123!";
        var hasher = new PasswordHasher();
        var dispatcher = new User(dispatcherId, new EmailAddress(dispatcherEmail), hasher.HashPassword(password), UserRole.Dispatcher, "Dispatcher W17 Fail");
        await context.Users.AddAsync(dispatcher);
        await context.SaveChangesAsync();

        using var factory = new DispatchTestWebApplicationFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(dispatcherEmail, password));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var authBody = await loginResp.Content.ReadFromJsonAsync<AuthSuccessResponse>();
        var token = authBody!.AccessToken;

        var clientRequestId = Guid.NewGuid().ToString();
        var nonExistentAreaId = Guid.NewGuid();

        // 首次请求传入不存在的 AreaId -> 业务失败 (404)
        var failedPayload = new CreateJobTaskRequest(
            Ref: GenerateRef("TSK-W17-FAIL"),
            Title: "W17 Failed Test Task",
            AreaId: nonExistentAreaId,
            ScheduledFor: new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(12)));

        var failReq = new HttpRequestMessage(HttpMethod.Post, "/api/dispatch/tasks")
        {
            Content = JsonContent.Create(failedPayload)
        };
        failReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        failReq.Headers.Add("X-Client-Request-Id", clientRequestId);

        var failResp = await client.SendAsync(failReq);
        failResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assert 1: 幂等记录不得残留（否则重试会被误判为已处理）
        await using (var verifyContext = _fixture.CreateDbContext())
        {
            var record = await verifyContext.IdempotencyRecords.FindAsync(clientRequestId);
            record.Should().BeNull();
        }

        // 第二次请求携带相同 ClientRequestId 但修正了 AreaId -> 应能正常成功
        var successPayload = new CreateJobTaskRequest(
            Ref: GenerateRef("TSK-W17-RETRY"),
            Title: "W17 Retried Test Task",
            AreaId: area.Id,
            ScheduledFor: new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.FromHours(12)));

        var retryReq = new HttpRequestMessage(HttpMethod.Post, "/api/dispatch/tasks")
        {
            Content = JsonContent.Create(successPayload)
        };
        retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        retryReq.Headers.Add("X-Client-Request-Id", clientRequestId);

        var retryResp = await client.SendAsync(retryReq);
        retryResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // 验证重试成功后幂等记录已写入
        await using (var verifyContext = _fixture.CreateDbContext())
        {
            var record = await verifyContext.IdempotencyRecords.FindAsync(clientRequestId);
            record.Should().NotBeNull();
            record!.StatusCode.Should().Be(200);
        }
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

public class DispatchTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public DispatchTestWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
            });
        });
    }
}
