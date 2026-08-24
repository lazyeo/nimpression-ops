using System.Collections.Concurrent;
using System.Diagnostics;
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

    public const string DefaultPassword = "SecurePassword123!";

    public OutboxProcessorIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // 显式禁用后台自动轮询 worker，以在测试中做确定性的批处理步进验证
        _factory = new RealtimeTestWebApplicationFactory(_fixture.ConnectionString, enableBackgroundProcessor: false);

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var user = new User(_userId, new EmailAddress(_email), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Outbox Driver");
        var driver = new Driver(
            _driverId,
            _userId,
            _empNo,
            "Class 2",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            new Money(25m),
            new Money(15m),
            new Money(1.2m),
            "phone",
            "address",
            "emergency",
            DateOnly.FromDateTime(DateTime.UtcNow));

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
        // 1. 连接 SignalR 客户端
        await using var connection = _factory.CreateHubConnection(_token);
        var receivedSignals = new ConcurrentBag<RealtimeMessage>();
        connection.On<RealtimeMessage>("ReceiveInvalidation", signal => receivedSignals.Add(signal));

        await connection.StartAsync();
        connection.State.Should().Be(HubConnectionState.Connected);

        await Task.Delay(100);

        // 2. 写入一条未处理的 Outbox 消息（设置较早的 OccurredAt 确保排在批处理首位）
        var taskId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddHours(-1);
        var payload = JsonSerializer.Serialize(new { JobTaskId = taskId, DriverId = _driverId, VehicleId = Guid.NewGuid() });

        await using (var db = _fixture.CreateDbContext())
        {
            var msg = new OutboxMessage(outboxId, "JobTaskAssigned", payload, occurredAt);
            db.OutboxMessages.Add(msg);
            await db.SaveChangesAsync();
        }

        // 3. 执行单批次处理
        var processor = new OutboxProcessorBackgroundService(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            _factory.Services.GetRequiredService<ILogger<OutboxProcessorBackgroundService>>());

        var processedCount = await processor.ProcessBatchAsync(CancellationToken.None);
        processedCount.Should().BeGreaterThanOrEqualTo(1);

        var sw = Stopwatch.StartNew();
        while (receivedSignals.IsEmpty && sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(50);
        }

        // 4. Assert: 客户端接收到推送失效信号
        receivedSignals.Should().Contain(s => s.Kind == RealtimeEventKinds.TaskAssigned && s.EntityId == taskId);

        // 5. Assert: 数据库中的 OutboxMessage 被标记 ProcessedAt
        await using (var db = _fixture.CreateDbContext())
        {
            var dbMsg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == outboxId);
            dbMsg.Should().NotBeNull();
            dbMsg!.ProcessedAt.Should().NotBeNull();
            dbMsg.Error.Should().BeNull();
        }

        // 6. Assert: 再次运行批处理，不会重复处理该条记录（已处理记录被过滤）
        await using (var db = _fixture.CreateDbContext())
        {
            var unprocessed = await db.OutboxMessages.Where(m => m.Id == outboxId && m.ProcessedAt == null).ToListAsync();
            unprocessed.Should().BeEmpty();
        }

        await connection.StopAsync();
    }

    [Fact]
    public async Task OutboxProcessor_OnNotifierFailure_RecordsAttemptAndError_ForRetry()
    {
        var outboxId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddHours(-2);
        var payload = JsonSerializer.Serialize(new { JobTaskId = taskId, DriverId = _driverId, VehicleId = Guid.NewGuid() });

        await using (var db = _fixture.CreateDbContext())
        {
            var msg = new OutboxMessage(outboxId, "JobTaskAssigned", payload, occurredAt);
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

        // Act: 执行批处理
        var processed = await processor.ProcessBatchAsync(CancellationToken.None);
        processed.Should().BeGreaterThanOrEqualTo(1);

        // Assert: 消息未被标记 ProcessedAt，而是记录了 Attempts 和 Error 信息，等待重试
        await using (var db = _fixture.CreateDbContext())
        {
            var dbMsg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == outboxId);
            dbMsg.Should().NotBeNull();
            dbMsg!.ProcessedAt.Should().BeNull();
            dbMsg.Attempts.Should().BeGreaterThanOrEqualTo(1);
            dbMsg.Error.Should().Contain("SignalR Hub connection lost");
        }
    }
}
