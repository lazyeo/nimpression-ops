using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Application.Features.Realtime.Common;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Seed;
using Nimpression.Infrastructure.Realtime.BackgroundServices;
using Nimpression.Infrastructure.Realtime.Services;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Nimpression.Integration.Tests.Realtime;

/// <summary>
/// Outbox 消费后台服务集成测试：
/// 验证至少一次投递、失败重试、标记 ProcessedAt 及避免重复投递的幂等性。
/// 采用完全确定性的测试驱动机制（测试宿主关闭后台自动轮询，由测试显式触发单条消息处理，并用 TaskCompletionSource 捕获 SignalR 事件）。
/// </summary>
[Collection("PostgreSqlCollection")]
public sealed class OutboxProcessorIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private RealtimeTestWebApplicationFactory _factory = null!;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _driverId = Guid.NewGuid();
    private readonly string _email = TestDataFactory.CreateEmail("rt_outbox_test");
    private readonly string _empNo = TestDataFactory.CreateEmployeeNo("OB");

    private string _token = null!;

    public const string DefaultPassword = "dev-only-insecure-password-123!";

    public OutboxProcessorIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // 显式禁用后台自动轮询 worker，以在测试中做确定性的测试驱动验证
        _factory = new RealtimeTestWebApplicationFactory(_fixture.ConnectionString, enableBackgroundProcessor: false);

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var user = new User(_userId, new EmailAddress(_email), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Outbox Driver");
        var driver = new Driver(
            _driverId,
            _userId,
            _empNo,
            "Class 2",
            SeedConstants.ReferenceDate.AddYears(1),
            new Money(25m),
            new Money(15m),
            new Money(1.2m),
            "phone",
            "address",
            "emergency",
            SeedConstants.ReferenceDate);

        context.Users.Add(user);
        context.Drivers.Add(driver);
        await context.SaveChangesAsync();

        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        _token = jwtGenerator.GenerateAccessToken(_userId, _email, "Driver", "Outbox Driver").Token;
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();

        try
        {
            await using var context = _fixture.CreateDbContext();
            var drivers = await context.Drivers.Where(d => d.Id == _driverId).ToListAsync();
            context.Drivers.RemoveRange(drivers);
            var users = await context.Users.Where(u => u.Id == _userId).ToListAsync();
            context.Users.RemoveRange(users);
            await context.SaveChangesAsync();
        }
        catch
        {
            // 忽略清理异常
        }
    }

    [Fact]
    public async Task OutboxProcessor_ProcessesUnprocessedMessages_DeliversToSignalR_AndMarksProcessedAt()
    {
        // 1. 连接 SignalR 客户端，使用 TaskCompletionSource 等待确定性消息到达
        var tcs = new TaskCompletionSource<RealtimeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = _factory.CreateHubConnection(_token);
        connection.On<RealtimeMessage>("ReceiveInvalidation", signal =>
        {
            if (signal.Kind == RealtimeEventKinds.TaskAssigned)
            {
                tcs.TrySetResult(signal);
            }
        });

        await connection.StartAsync();
        await connection.InvokeAsync("Ping");
        connection.State.Should().Be(HubConnectionState.Connected);

        // 2. 写入一条未处理的 Outbox 消息
        var taskId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { JobTaskId = taskId, DriverId = _driverId, VehicleId = Guid.NewGuid() });

        await using (var db = _fixture.CreateDbContext())
        {
            var msg = new OutboxMessage(outboxId, "JobTaskAssigned", payload, new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));
            db.OutboxMessages.Add(msg);
            await db.SaveChangesAsync();
        }

        // 3. 由测试直接触发对该条消息的处理（测试驱动，无定时器/无轮询竞态）
        var processor = new OutboxProcessorBackgroundService(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            _factory.Services.GetRequiredService<ILogger<OutboxProcessorBackgroundService>>());

        var processed = await processor.ProcessMessageAsync(outboxId);
        processed.Should().BeTrue();

        // 4. Assert: 确定性等待 SignalR 客户端收到推送失效信号（非 Task.Delay）
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var receivedSignal = await tcs.Task.WaitAsync(cts.Token);
        receivedSignal.Should().NotBeNull();
        receivedSignal.EntityId.Should().Be(taskId);
        receivedSignal.Kind.Should().Be(RealtimeEventKinds.TaskAssigned);

        // 5. Assert: 数据库中的 OutboxMessage 被标记 ProcessedAt
        await using (var db = _fixture.CreateDbContext())
        {
            var dbMsg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == outboxId);
            dbMsg.Should().NotBeNull();
            dbMsg!.ProcessedAt.Should().NotBeNull();
            dbMsg.Error.Should().BeNull();
        }

        // 6. Assert: 再次运行，该已处理消息不会被重复处理
        var processedAgain = await processor.ProcessMessageAsync(outboxId);
        processedAgain.Should().BeFalse();

        await connection.StopAsync();
    }

    [Fact]
    public async Task OutboxProcessor_OnNotifierFailure_RecordsAttemptAndError_ForRetry()
    {
        var outboxId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { JobTaskId = taskId, DriverId = _driverId, VehicleId = Guid.NewGuid() });

        await using (var db = _fixture.CreateDbContext())
        {
            var msg = new OutboxMessage(outboxId, "JobTaskAssigned", payload, new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));
            db.OutboxMessages.Add(msg);
            await db.SaveChangesAsync();
        }

        // 构造一个模拟故障的 Notifier
        var failingNotifier = Substitute.For<IRealtimeNotifier>();
        failingNotifier.PublishToGroupAsync(Arg.Any<string>(), Arg.Any<RealtimeMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SignalR Hub connection lost"));

        var services = new ServiceCollection();
        services.AddScoped(_ => _fixture.CreateDbContext());
        services.AddSingleton<IOutboxToRealtimeMapper, OutboxToRealtimeMapper>();
        services.AddSingleton(failingNotifier);
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = Substitute.For<ILogger<OutboxProcessorBackgroundService>>();

        var processor = new OutboxProcessorBackgroundService(scopeFactory, logger);

        // Act: 显式触发该条消息的处理
        var processed = await processor.ProcessMessageAsync(outboxId);
        processed.Should().BeTrue();

        // Assert: 消息未被标记 ProcessedAt，而是记录了 Attempts 和 Error 信息，等待重试
        await using (var db = _fixture.CreateDbContext())
        {
            var dbMsg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == outboxId);
            dbMsg.Should().NotBeNull();
            dbMsg!.ProcessedAt.Should().BeNull();
            dbMsg.Attempts.Should().Be(1);
            dbMsg.Error.Should().Contain("SignalR Hub connection lost");
        }
    }
}
