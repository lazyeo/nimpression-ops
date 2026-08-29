using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
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
/// F12.4 传输协议降级验收测试：
/// 显式配置并验证 SignalR 传输协议降级（WebSocket 不可用时回落至 Long Polling），
/// 在受限网络或降级场景下均能稳定建立连接并实时接收失效信号，业务功能无任何降级。
/// 采用 TaskCompletionSource 确定性信号捕获，消除任何时序竞态。
/// </summary>
[Collection("PostgreSqlCollection")]
public sealed class F12_4_TransportFallbackIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private RealtimeTestWebApplicationFactory _factory = null!;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _driverId = Guid.NewGuid();
    private readonly string _email = TestDataFactory.CreateEmail("rt_fallback");
    private readonly string _empNo = TestDataFactory.CreateEmployeeNo("FB");

    private string _token = null!;

    public const string DefaultPassword = "SecurePassword123!";

    public F12_4_TransportFallbackIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new RealtimeTestWebApplicationFactory(_fixture.ConnectionString, enableBackgroundProcessor: false);

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var user = new User(_userId, new EmailAddress(_email), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Fallback Driver");
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
        _token = jwtGenerator.GenerateAccessToken(_userId, _email, "Driver", "Fallback Driver").Token;
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
    public async Task F12_4_LongPollingFallback_DeliversInvalidationSignal_WithoutFunctionalDegradation()
    {
        var taskId = Guid.NewGuid();
        var expectedMessage = new RealtimeMessage(RealtimeEventKinds.TaskAssigned, taskId, DateTimeOffset.UtcNow);

        var tcs = new TaskCompletionSource<RealtimeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Arrange: 显式模拟 WebSocket 不可用场景，强制回落至 LongPolling 传输协议
        await using var connection = _factory.CreateHubConnection(_token, HttpTransportType.LongPolling);

        connection.On<RealtimeMessage>("ReceiveInvalidation", msg =>
        {
            if (msg.EntityId == taskId)
            {
                tcs.TrySetResult(msg);
            }
        });

        await connection.StartAsync();
        await connection.InvokeAsync("Ping");
        connection.State.Should().Be(HubConnectionState.Connected);

        using var scope = _factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();

        // Act: 向客户端广播失效信号
        await notifier.PublishToDriverAsync(_driverId, expectedMessage);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var receivedMessage = await tcs.Task.WaitAsync(cts.Token);

        // Assert: 降级传输模式下完整接收纯失效信号，功能与实时性无损
        receivedMessage.Should().NotBeNull();
        receivedMessage.EntityId.Should().Be(taskId);
        receivedMessage.Kind.Should().Be(RealtimeEventKinds.TaskAssigned);

        await connection.StopAsync();
    }
}
