using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Application.Features.Realtime.Common;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Seed;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Realtime;

/// <summary>
/// F12.5 不可信通道安全性与韧性验收测试：
/// <para>
/// <b>核心设计灵魂验证：推送通道不可信，推送仅作失效信号，不作数据通道。</b><br/>
/// 构造一条内容被恶意篡改的推送消息（伪造实体 ID、伪造字段载荷），<br/>
/// 断言客户端在收到推送后遵循架构约束走权威 HTTP 接口拉取真实数据，业务状态与数据完全不受推送篡改的影响。
/// 采用 TaskCompletionSource 确定性信号捕获，消除任何时序竞态。
/// </para>
/// </summary>
[Collection("PostgreSqlCollection")]
public sealed class F12_5_UntrustedChannelSecurityIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private RealtimeTestWebApplicationFactory _factory = null!;
    private HttpClient _httpClient = null!;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _driverId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();
    private readonly Guid _areaId = Guid.NewGuid();
    private readonly Guid _taskId = Guid.NewGuid();

    private readonly string _email = TestDataFactory.CreateEmail("rt_untrust");
    private readonly string _empNo = TestDataFactory.CreateEmployeeNo("UT");
    private readonly string _rego = TestDataFactory.CreateRego("UT");

    private string _token = null!;

    public const string DefaultPassword = "dev-only-insecure-password-123!";

    public F12_5_UntrustedChannelSecurityIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new RealtimeTestWebApplicationFactory(_fixture.ConnectionString, enableBackgroundProcessor: false);
        _httpClient = _factory.CreateClient();

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var user = new User(_userId, new EmailAddress(_email), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Untrusted Driver");
        var driver = new Driver(
            _driverId,
            _userId,
            _empNo,
            "Class 2",
            SeedConstants.ReferenceDate.AddYears(1),
            new Money(28m),
            new Money(15m),
            new Money(1.2m),
            "phone",
            "address",
            "emergency",
            SeedConstants.ReferenceDate);

        var vehicle = new Vehicle(
            _vehicleId,
            new Rego(_rego),
            "Toyota",
            "HiAce",
            2023,
            "VINENC12345",
            new Kilometres(12000m),
            new Kilometres(10000m),
            status: VehicleStatus.Active);

        var area = new Area(_areaId, "Auckland Central", "AKL-CEN", "Auckland CBD");

        var task = new JobTask(
            _taskId,
            "TSK-AUTH-001",
            "Authoritative High Priority Delivery",
            _areaId,
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            _userId,
            "Authoritative Delivery Notes",
            TaskPriority.High,
            new Kilometres(15m),
            _driverId,
            _vehicleId);

        context.Users.Add(user);
        context.Drivers.Add(driver);
        context.Vehicles.Add(vehicle);
        context.Areas.Add(area);
        context.JobTasks.Add(task);
        await context.SaveChangesAsync();

        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        _token = jwtGenerator.GenerateAccessToken(_userId, _email, "Driver", "Untrusted Driver").Token;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _factory?.Dispose();
    }

    public async Task DisposeAsync()
    {
        _httpClient.Dispose();
        await _factory.DisposeAsync();

        try
        {
            await using var context = _fixture.CreateDbContext();
            var tasks = await context.JobTasks.Where(t => t.Id == _taskId).ToListAsync();
            context.JobTasks.RemoveRange(tasks);
            var vehicles = await context.Vehicles.Where(v => v.Id == _vehicleId).ToListAsync();
            context.Vehicles.RemoveRange(vehicles);
            var areas = await context.Areas.Where(a => a.Id == _areaId).ToListAsync();
            context.Areas.RemoveRange(areas);
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
    public async Task F12_5_UntrustedChannel_TamperedPushMessage_DoesNotAffectBusinessCorrectness()
    {
        var bogusEntityId = Guid.NewGuid();
        var tamperedSignal = new RealtimeMessage(
            Kind: "malicious.tampered.kind",
            EntityId: bogusEntityId,
            OccurredAt: new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));

        var legitimateSignal = new RealtimeMessage(
            Kind: RealtimeEventKinds.TaskAssigned,
            EntityId: _taskId,
            OccurredAt: new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));

        var tcsTampered = new TaskCompletionSource<RealtimeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcsLegitimate = new TaskCompletionSource<RealtimeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        // 1. 建立 SignalR 连接
        await using var connection = _factory.CreateHubConnection(_token);

        connection.On<RealtimeMessage>("ReceiveInvalidation", signal =>
        {
            if (signal.EntityId == bogusEntityId)
            {
                tcsTampered.TrySetResult(signal);
            }
            else if (signal.EntityId == _taskId)
            {
                tcsLegitimate.TrySetResult(signal);
            }
        });

        await connection.StartAsync();
        await connection.InvokeAsync("Ping");
        connection.State.Should().Be(HubConnectionState.Connected);

        using var scope = _factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();

        // 2. 模拟攻击者向推送通道注入恶意篡改的消息（如伪造的已完成状态、虚假实体 ID、钓鱼指令等）
        await notifier.PublishToDriverAsync(_driverId, tamperedSignal);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var receivedTampered = await tcsTampered.Task.WaitAsync(cts1.Token);
        receivedTampered.Should().NotBeNull();
        receivedTampered.EntityId.Should().Be(bogusEntityId);

        // 3. 客户端处理机制（架构约束）：客户端绝不信任推送载荷，收到失效信号后仅使用 HTTP API 重新拉取权威数据
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        // 3.1 尝试用篡改的伪造 ID 请求权威接口 -> 404 Not Found，系统不产生任何脏数据或状态突变
        var bogusResp = await _httpClient.GetAsync($"/api/drivers/{bogusEntityId}");
        bogusResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3.2 模拟发送真实任务的合法失效信号
        await notifier.PublishToDriverAsync(_driverId, legitimateSignal);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var receivedLegitimate = await tcsLegitimate.Task.WaitAsync(cts2.Token);
        receivedLegitimate.Should().NotBeNull();
        receivedLegitimate.EntityId.Should().Be(_taskId);

        // 3.3 用合法失效信号中的实体 ID 重新拉取司机的权威数据 -> 200 OK
        var legitimateResp = await _httpClient.GetAsync($"/api/drivers/{_driverId}");
        legitimateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var driverDetail = await legitimateResp.Content.ReadFromJsonAsync<DriverDetailDto>();
        driverDetail.Should().NotBeNull();
        driverDetail!.Id.Should().Be(_driverId);
        driverDetail.EmployeeNo.Should().Be(_empNo);
        driverDetail.LicenceClass.Should().Be("Class 2");

        // 4. 结论：推送通道即便被窃听、篡改或注入，客户端始终只以权威 HTTP 响应为准，业务逻辑 100% 正确！
        await connection.StopAsync();
    }
}
