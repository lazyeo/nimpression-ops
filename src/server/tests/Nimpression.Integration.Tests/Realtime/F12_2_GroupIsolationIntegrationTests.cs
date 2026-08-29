using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Application.Features.Realtime.Common;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Realtime;

/// <summary>
/// F12.2 分组隔离验收测试：
/// 按角色与司机 Id 实施强分组隔离；司机 A 绝对收不到发给司机 B 的私有消息。
/// 本测试使用两个独立的 SignalR 客户端连接在真实运行环境中实证隔离性。
/// 采用 TaskCompletionSource 确定性信号捕获，消除任何时序竞态。
/// </summary>
[Collection("PostgreSqlCollection")]
public sealed class F12_2_GroupIsolationIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private RealtimeTestWebApplicationFactory _factory = null!;

    private readonly Guid _userAId = Guid.NewGuid();
    private readonly Guid _userBId = Guid.NewGuid();
    private readonly Guid _userDispatcherId = Guid.NewGuid();

    private readonly Guid _driverAId = Guid.NewGuid();
    private readonly Guid _driverBId = Guid.NewGuid();

    private readonly string _emailA = TestDataFactory.CreateEmail("rt_iso_drva");
    private readonly string _emailB = TestDataFactory.CreateEmail("rt_iso_drvb");
    private readonly string _emailDispatcher = TestDataFactory.CreateEmail("rt_iso_dsp");

    private readonly string _empNoA = TestDataFactory.CreateEmployeeNo("DA");
    private readonly string _empNoB = TestDataFactory.CreateEmployeeNo("DB");

    private string _tokenA = null!;
    private string _tokenB = null!;
    private string _tokenDispatcher = null!;

    public const string DefaultPassword = "SecurePassword123!";

    public F12_2_GroupIsolationIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new RealtimeTestWebApplicationFactory(_fixture.ConnectionString, enableBackgroundProcessor: false);

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var userA = new User(_userAId, new EmailAddress(_emailA), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Alice");
        var userB = new User(_userBId, new EmailAddress(_emailB), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Bob");
        var userDsp = new User(_userDispatcherId, new EmailAddress(_emailDispatcher), _passwordHasher.HashPassword(DefaultPassword), UserRole.Dispatcher, "Dispatcher Dan");

        var driverA = new Driver(
            _driverAId,
            _userAId,
            _empNoA,
            "Class 2",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            new Money(28m),
            new Money(15m),
            new Money(1.2m),
            "phoneA",
            "addressA",
            "emergencyA",
            DateOnly.FromDateTime(DateTime.UtcNow));

        var driverB = new Driver(
            _driverBId,
            _userBId,
            _empNoB,
            "Class 2",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            new Money(28m),
            new Money(15m),
            new Money(1.2m),
            "phoneB",
            "addressB",
            "emergencyB",
            DateOnly.FromDateTime(DateTime.UtcNow));

        context.Users.AddRange(userA, userB, userDsp);
        context.Drivers.AddRange(driverA, driverB);
        await context.SaveChangesAsync();

        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        _tokenA = jwtGenerator.GenerateAccessToken(_userAId, _emailA, "Driver", "Driver Alice").Token;
        _tokenB = jwtGenerator.GenerateAccessToken(_userBId, _emailB, "Driver", "Driver Bob").Token;
        _tokenDispatcher = jwtGenerator.GenerateAccessToken(_userDispatcherId, _emailDispatcher, "Dispatcher", "Dispatcher Dan").Token;
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
            var drivers = await context.Drivers
                .Where(d => d.Id == _driverAId || d.Id == _driverBId)
                .ToListAsync();
            context.Drivers.RemoveRange(drivers);

            var users = await context.Users
                .Where(u => u.Id == _userAId || u.Id == _userBId || u.Id == _userDispatcherId)
                .ToListAsync();
            context.Users.RemoveRange(users);

            await context.SaveChangesAsync();
        }
        catch
        {
            // 忽略清理异常
        }
    }

    [Fact]
    public async Task F12_2_DriverA_DoesNotReceive_MessagesSentToDriverB()
    {
        // Arrange: 建立两个独立的 SignalR 客户端连接，分别作为 司机 A 与 司机 B
        await using var connectionA = _factory.CreateHubConnection(_tokenA);
        await using var connectionB = _factory.CreateHubConnection(_tokenB);

        var receivedMessagesA = new ConcurrentBag<RealtimeMessage>();
        var receivedMessagesB = new ConcurrentBag<RealtimeMessage>();

        var taskAId = Guid.NewGuid();
        var taskBId = Guid.NewGuid();

        var tcsA = new TaskCompletionSource<RealtimeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcsB = new TaskCompletionSource<RealtimeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        connectionA.On<RealtimeMessage>("ReceiveInvalidation", msg =>
        {
            receivedMessagesA.Add(msg);
            if (msg.EntityId == taskAId)
            {
                tcsA.TrySetResult(msg);
            }
        });

        connectionB.On<RealtimeMessage>("ReceiveInvalidation", msg =>
        {
            receivedMessagesB.Add(msg);
            if (msg.EntityId == taskBId)
            {
                tcsB.TrySetResult(msg);
            }
        });

        await connectionA.StartAsync();
        await connectionA.InvokeAsync("Ping");

        await connectionB.StartAsync();
        await connectionB.InvokeAsync("Ping");

        // 验证连接已正常建立
        connectionA.State.Should().Be(HubConnectionState.Connected);
        connectionB.State.Should().Be(HubConnectionState.Connected);

        using var scope = _factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();

        var messageForDriverA = new RealtimeMessage(RealtimeEventKinds.TaskAssigned, taskAId, DateTimeOffset.UtcNow);
        var messageForDriverB = new RealtimeMessage(RealtimeEventKinds.TaskAssigned, taskBId, DateTimeOffset.UtcNow);

        // Act 1: 向司机 A 专属群组推送消息
        await notifier.PublishToDriverAsync(_driverAId, messageForDriverA);

        using var ctsA = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var msgA = await tcsA.Task.WaitAsync(ctsA.Token);
        msgA.EntityId.Should().Be(taskAId);

        // Assert 1: 司机 A 收到消息，司机 B 绝对收不到发给司机 A 的消息
        receivedMessagesA.Should().ContainSingle(m => m.EntityId == taskAId && m.Kind == RealtimeEventKinds.TaskAssigned);
        receivedMessagesB.Should().BeEmpty();

        // Act 2: 向司机 B 专属群组推送消息
        await notifier.PublishToDriverAsync(_driverBId, messageForDriverB);

        using var ctsB = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var msgB = await tcsB.Task.WaitAsync(ctsB.Token);
        msgB.EntityId.Should().Be(taskBId);

        // Assert 2: 司机 B 收到属于自己的消息，司机 A 消息列表无变动（仍仅有此前的一条）
        receivedMessagesB.Should().ContainSingle(m => m.EntityId == taskBId && m.Kind == RealtimeEventKinds.TaskAssigned);
        receivedMessagesA.Should().HaveCount(1);

        await connectionA.StopAsync();
        await connectionB.StopAsync();
    }

    [Fact]
    public async Task F12_2_RoleIsolation_VehicleAlert_ReceivedByDispatcher_NotReceivedByDriver()
    {
        // Arrange: 建立 司机 A 与 调度员 两个独立连接
        await using var driverConnection = _factory.CreateHubConnection(_tokenA);
        await using var dispatcherConnection = _factory.CreateHubConnection(_tokenDispatcher);

        var driverMessages = new ConcurrentBag<RealtimeMessage>();
        var dispatcherMessages = new ConcurrentBag<RealtimeMessage>();

        var vehicleId = Guid.NewGuid();
        var tcsDispatcher = new TaskCompletionSource<RealtimeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        driverConnection.On<RealtimeMessage>("ReceiveInvalidation", msg => driverMessages.Add(msg));
        dispatcherConnection.On<RealtimeMessage>("ReceiveInvalidation", msg =>
        {
            dispatcherMessages.Add(msg);
            if (msg.EntityId == vehicleId)
            {
                tcsDispatcher.TrySetResult(msg);
            }
        });

        await driverConnection.StartAsync();
        await driverConnection.InvokeAsync("Ping");

        await dispatcherConnection.StartAsync();
        await dispatcherConnection.InvokeAsync("Ping");

        using var scope = _factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();

        var alertMessage = new RealtimeMessage(RealtimeEventKinds.VehicleServiceThresholdReached, vehicleId, DateTimeOffset.UtcNow);

        // Act: 向 Dispatcher 角色组广播车辆保养报警
        await notifier.PublishToRoleAsync(UserRole.Dispatcher.ToString(), alertMessage);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var alertReceived = await tcsDispatcher.Task.WaitAsync(cts.Token);
        alertReceived.EntityId.Should().Be(vehicleId);

        // Assert: 调度员成功接收失效信号，司机端未接收到任何该角色消息
        dispatcherMessages.Should().ContainSingle(m => m.EntityId == vehicleId && m.Kind == RealtimeEventKinds.VehicleServiceThresholdReached);
        driverMessages.Should().BeEmpty();

        await driverConnection.StopAsync();
        await dispatcherConnection.StopAsync();
    }
}
