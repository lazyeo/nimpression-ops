using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Realtime.Common;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Seed;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Realtime;

/// <summary>
/// F12.3 断线重连与增量补齐验收测试：
/// 支持自动重连；重连后调用服务端变更端点拉取断线期间遗漏的全部失效信号，补齐数据状态。
/// </summary>
[Collection("PostgreSqlCollection")]
public sealed class F12_3_ReconnectionAndIncrementalCatchUpIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private RealtimeTestWebApplicationFactory _factory = null!;
    private HttpClient _httpClient = null!;

    private readonly Guid _driverAUserId = Guid.NewGuid();
    private readonly Guid _driverBUserId = Guid.NewGuid();
    private readonly Guid _driverAId = Guid.NewGuid();
    private readonly Guid _driverBId = Guid.NewGuid();

    private readonly string _emailA = TestDataFactory.CreateEmail("rt_catchup_a");
    private readonly string _emailB = TestDataFactory.CreateEmail("rt_catchup_b");
    private readonly string _empNoA = TestDataFactory.CreateEmployeeNo("CA");
    private readonly string _empNoB = TestDataFactory.CreateEmployeeNo("CB");

    private string _tokenA = null!;

    public const string DefaultPassword = "dev-only-insecure-password-123!";

    public F12_3_ReconnectionAndIncrementalCatchUpIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new RealtimeTestWebApplicationFactory(_fixture.ConnectionString);
        _httpClient = _factory.CreateClient();

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var userA = new User(_driverAUserId, new EmailAddress(_emailA), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "CatchUp Driver A");
        var userB = new User(_driverBUserId, new EmailAddress(_emailB), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "CatchUp Driver B");

        var driverA = new Driver(
            _driverAId,
            _driverAUserId,
            _empNoA,
            "Class 2",
            SeedConstants.ReferenceDate.AddYears(1),
            new Money(25m),
            new Money(15m),
            new Money(1.2m),
            "phoneA",
            "addressA",
            "emergencyA",
            SeedConstants.ReferenceDate);

        var driverB = new Driver(
            _driverBId,
            _driverBUserId,
            _empNoB,
            "Class 2",
            SeedConstants.ReferenceDate.AddYears(1),
            new Money(25m),
            new Money(15m),
            new Money(1.2m),
            "phoneB",
            "addressB",
            "emergencyB",
            SeedConstants.ReferenceDate);

        context.Users.AddRange(userA, userB);
        context.Drivers.AddRange(driverA, driverB);
        await context.SaveChangesAsync();

        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        _tokenA = jwtGenerator.GenerateAccessToken(_driverAUserId, _emailA, "Driver", "CatchUp Driver A").Token;
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
            var drivers = await context.Drivers
                .Where(d => d.Id == _driverAId || d.Id == _driverBId)
                .ToListAsync();
            context.Drivers.RemoveRange(drivers);

            var users = await context.Users
                .Where(u => u.Id == _driverAUserId || u.Id == _driverBUserId)
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
    public async Task F12_3_IncrementalCatchUp_PullsMissedChangesSinceDisconnectionTimestamp()
    {
        // 1. 模拟司机在线并正常接收初始事件
        await using var connection = _factory.CreateHubConnection(_tokenA);
        await connection.StartAsync();
        connection.State.Should().Be(HubConnectionState.Connected);

        // 2. 模拟发生网络波动，客户端断开连接，记录断线时间点
        var disconnectionTime = new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
        await connection.StopAsync();
        connection.State.Should().Be(HubConnectionState.Disconnected);

        // 3. 在断线期间，系统产生多条领域事件写入 Outbox
        var taskA1Id = Guid.NewGuid();
        var taskA2Id = Guid.NewGuid();
        var taskB1Id = Guid.NewGuid();
        var newsId = Guid.NewGuid();

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var outboxTaskA1 = new OutboxMessage(
                Guid.NewGuid(),
                "JobTaskAssigned",
                JsonSerializer.Serialize(new { JobTaskId = taskA1Id, DriverId = _driverAId, VehicleId = Guid.NewGuid() }),
                disconnectionTime.AddMilliseconds(100));

            var outboxTaskB1 = new OutboxMessage(
                Guid.NewGuid(),
                "JobTaskAssigned",
                JsonSerializer.Serialize(new { JobTaskId = taskB1Id, DriverId = _driverBId, VehicleId = Guid.NewGuid() }),
                disconnectionTime.AddMilliseconds(200));

            var outboxNews = new OutboxMessage(
                Guid.NewGuid(),
                "NewsPublished",
                JsonSerializer.Serialize(new { NewsPostId = newsId, Audience = (int)NewsAudience.All }),
                disconnectionTime.AddMilliseconds(300));

            var outboxTaskA2 = new OutboxMessage(
                Guid.NewGuid(),
                "JobTaskCompleted",
                JsonSerializer.Serialize(new { JobTaskId = taskA2Id, DriverId = _driverAId }),
                disconnectionTime.AddMilliseconds(400));

            dbContext.OutboxMessages.AddRange(outboxTaskA1, outboxTaskB1, outboxNews, outboxTaskA2);
            await dbContext.SaveChangesAsync();
        }

        // 4. 司机重连后，通过 HTTP 端点请求 since 之后的增量变更列表
        var sinceParam = Uri.EscapeDataString(disconnectionTime.ToString("o"));
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/realtime/changes?since={sinceParam}&limit=100");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenA);

        var response = await _httpClient.SendAsync(request);

        // Assert: 成功获取增量列表
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var changes = await response.Content.ReadFromJsonAsync<List<RealtimeChangeDto>>();
        changes.Should().NotBeNull();

        // 验证：仅包含司机 A 相关的两个任务失效信号及公共广播，绝不包含司机 B 的私有任务（F12.2 + F12.3）
        changes!.Should().Contain(c => c.Kind == RealtimeEventKinds.TaskAssigned && c.EntityId == taskA1Id);
        changes.Should().Contain(c => c.Kind == RealtimeEventKinds.TaskCompleted && c.EntityId == taskA2Id);
        changes.Should().Contain(c => c.Kind == RealtimeEventKinds.NewsPublished && c.EntityId == newsId);

        changes.Should().NotContain(c => c.EntityId == taskB1Id);
    }
}
