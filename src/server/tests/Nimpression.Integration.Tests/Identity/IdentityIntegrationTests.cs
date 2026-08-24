using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Api.Endpoints;
using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Identity;

[Collection("PostgreSqlCollection")]
public sealed class IdentityIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private IdentityTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _dispatcherId = Guid.NewGuid();
    private readonly Guid _driver1Id = Guid.NewGuid();
    private readonly Guid _driver2Id = Guid.NewGuid();

    private readonly string _adminEmail = TestDataFactory.CreateEmail("id_admin");
    private readonly string _dispatcherEmail = TestDataFactory.CreateEmail("id_dispatch");
    private readonly string _driver1Email = TestDataFactory.CreateEmail("id_driver1");
    private readonly string _driver2Email = TestDataFactory.CreateEmail("id_driver2");

    public const string DefaultPassword = "SecurePassword123!";

    public IdentityIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new IdentityTestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("http://localhost")
        });

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var admin = new User(_adminId, new EmailAddress(_adminEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Admin, "Admin Boss");
        var dispatcher = new User(_dispatcherId, new EmailAddress(_dispatcherEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Dispatcher, "Dispatcher Dan");
        var driver1 = new User(_driver1Id, new EmailAddress(_driver1Email), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Dave");
        var driver2 = new User(_driver2Id, new EmailAddress(_driver2Email), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Bob");

        context.Users.AddRange(admin, dispatcher, driver1, driver2);
        await context.SaveChangesAsync();

        LoginRateLimiter.Reset();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        try
        {
            await using var context = _fixture.CreateDbContext();
            var testUserIds = new[] { _adminId, _dispatcherId, _driver1Id, _driver2Id };
            var existingTokens = await context.RefreshTokens
                .Where(rt => testUserIds.Contains(rt.UserId))
                .ToListAsync();
            context.RefreshTokens.RemoveRange(existingTokens);

            var existingUsers = await context.Users
                .Where(u => testUserIds.Contains(u.Id))
                .ToListAsync();
            context.Users.RemoveRange(existingUsers);

            await context.SaveChangesAsync();
        }
        catch
        {
            // 忽略清理异常，避免掩盖断言错误
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task F1_1_Login_WithValidCredentials_Returns200_AccessToken_AndHttpOnlyCookie()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(_adminEmail, DefaultPassword));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthSuccessResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.ExpiresIn.Should().Be(900);
        body.User.Email.Should().Be(_adminEmail);
        body.User.Role.Should().Be(UserRole.Admin);

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookieHeader = string.Join(";", cookies!);
        cookieHeader.Should().Contain("refreshToken=");
        cookieHeader.Should().Contain("httponly");
        cookieHeader.Should().Contain("path=/api/auth");
    }

    [Fact]
    public async Task F1_1_Login_WithNonExistentUser_Returns401_WithUnifiedResponseBody()
    {
        // Act
        var nonExistentEmail = TestDataFactory.CreateEmail("ghost");
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(nonExistentEmail, "WrongPass123!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("AUTH_INVALID_CREDENTIALS");
        json.Should().Contain("Invalid email or password.");
    }

    [Fact]
    public async Task F1_1_Login_WithWrongPassword_Returns401_WithIdenticalResponseBody()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(_adminEmail, "WrongPass123!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("AUTH_INVALID_CREDENTIALS");
        json.Should().Contain("Invalid email or password.");
    }

    [Fact]
    public async Task F1_1_Login_TimingSideChannel_NonExistentVsWrongPassword_MedianLatencyDifferenceWithinThreshold()
    {
        // 预热请求（确保 JIT 编译及连接池建立）
        for (var w = 0; w < 3; w++)
        {
            var warmupReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(TestDataFactory.CreateEmail($"warmup_{w}"), "WrongPass123!"))
            };
            warmupReq1.Headers.Add("X-Forwarded-For", $"10.0.0.{w + 1}");
            await _client.SendAsync(warmupReq1);

            var warmupReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(_adminEmail, "WrongPass123!"))
            };
            warmupReq2.Headers.Add("X-Forwarded-For", $"10.0.1.{w + 1}");
            await _client.SendAsync(warmupReq2);
        }

        const int iterations = 20;
        var nonExistentLatencies = new List<double>(iterations);
        var wrongPasswordLatencies = new List<double>(iterations);

        // 交替发起请求测量耗时（消除执行先后顺序带来的系统 CPU/连接负载差异）
        for (var i = 0; i < iterations; i++)
        {
            // 1. 不存在邮箱请求
            var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(TestDataFactory.CreateEmail($"ghost_{i}"), "WrongPass123!"))
            };
            req1.Headers.Add("X-Forwarded-For", $"10.100.{i / 250}.{i % 250 + 1}");

            var sw1 = Stopwatch.StartNew();
            var resp1 = await _client.SendAsync(req1);
            sw1.Stop();

            resp1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            nonExistentLatencies.Add(sw1.Elapsed.TotalMilliseconds);

            // 2. 存在用户但密码错误请求
            var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(_adminEmail, $"WrongPassword_{i}!"))
            };
            req2.Headers.Add("X-Forwarded-For", $"10.200.{i / 250}.{i % 250 + 1}");

            var sw2 = Stopwatch.StartNew();
            var resp2 = await _client.SendAsync(req2);
            sw2.Stop();

            resp2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            wrongPasswordLatencies.Add(sw2.Elapsed.TotalMilliseconds);
        }

        // 计算中位数（Median）
        nonExistentLatencies.Sort();
        wrongPasswordLatencies.Sort();

        var nonExistentMedian = (nonExistentLatencies[9] + nonExistentLatencies[10]) / 2.0;
        var wrongPasswordMedian = (wrongPasswordLatencies[9] + wrongPasswordLatencies[10]) / 2.0;

        var diff = Math.Abs(nonExistentMedian - wrongPasswordMedian);

        // 断言：两条路径均执行了完整的 BCrypt workFactor=12 哈希校验（耗时合理），且中位耗时差异在 50ms 以内
        nonExistentMedian.Should().BeGreaterThan(50.0);
        wrongPasswordMedian.Should().BeGreaterThan(50.0);
        diff.Should().BeLessThanOrEqualTo(50.0, $"中位耗时差异 {diff:F2}ms 应当在 50ms 阈值内 (不存在={nonExistentMedian:F2}ms, 密码错={wrongPasswordMedian:F2}ms)");
    }

    [Fact]
    public async Task F1_2_RefreshToken_Rotation_ReturnsNewTokens_AndRevokesOldToken()
    {
        // 1. 登录拿 token
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(_driver1Email, DefaultPassword));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResp.Content.ReadFromJsonAsync<AuthSuccessResponse>();

        // 2. 刷新（通过 Cookie 或 Body 轮转）
        var refreshResp = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(null));
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refreshResp.Content.ReadFromJsonAsync<AuthSuccessResponse>();

        refreshBody!.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshBody.AccessToken.Should().NotBe(loginBody!.AccessToken);

        // 验证数据库中旧 token 已被撤销，新 token 处于活跃态
        await using var context = _fixture.CreateDbContext();
        var tokens = await context.RefreshTokens.Where(rt => rt.UserId == _driver1Id).ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Count(t => t.IsRevoked).Should().Be(1);
    }

    [Fact]
    public async Task F1_2_RefreshToken_ReplayAttack_RevokesAllActiveTokensForUser_AndRecordsAuditEvent()
    {
        // 1. 登录拿原始 raw token
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(_driver1Email, DefaultPassword));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookies = loginResp.Headers.GetValues("Set-Cookie").ToList();
        var rawCookie = cookies.First(c => c.StartsWith("refreshToken=", StringComparison.Ordinal));
        var originalRefreshToken = rawCookie.Split(';')[0].Replace("refreshToken=", "");

        // 2. 正常刷新一次（旧 token 被轮转并作废）
        var refreshResp1 = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(originalRefreshToken));
        refreshResp1.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. 模拟攻击者重放第一次已作废的旧 token
        using var attackerClient = _factory.CreateClient();
        var replayResp = await attackerClient.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(originalRefreshToken));

        // Assert: 拒绝访问
        replayResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 验证该用户的所有刷新令牌均被彻底撤销（防重放级联保护）
        await using var context = _fixture.CreateDbContext();
        var activeTokens = await context.RefreshTokens.Where(rt => rt.UserId == _driver1Id && rt.RevokedAt == null).ToListAsync();
        activeTokens.Should().BeEmpty();

        // 验证记录了安全审计日志
        var audit = await context.AuditEvents.FirstOrDefaultAsync(a => a.Action == "Security.RefreshTokenReplayDetected" && a.EntityId == _driver1Id.ToString());
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task F1_3_Logout_RevokesToken_AndClearsCookie_SubsequentRefreshReturns401()
    {
        // 1. 登录
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(_driver1Email, DefaultPassword));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookies = loginResp.Headers.GetValues("Set-Cookie").ToList();
        var rawCookie = cookies.First(c => c.StartsWith("refreshToken=", StringComparison.Ordinal));
        var rawRefreshToken = rawCookie.Split(';')[0].Replace("refreshToken=", "");

        // 2. 登出
        var logoutResp = await _client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(rawRefreshToken));
        logoutResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. 再次尝试刷新
        var refreshResp = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(rawRefreshToken));
        refreshResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task F1_4_RoleAuthorizationMatrix_3Roles_5Endpoints_15Assertions()
    {
        // 获取三个角色的 Access Token
        var adminToken = await GetAccessTokenAsync(_adminEmail, DefaultPassword);
        var dispatcherToken = await GetAccessTokenAsync(_dispatcherEmail, DefaultPassword);
        var driverToken = await GetAccessTokenAsync(_driver1Email, DefaultPassword);

        // 端点列表定义：
        // 1. POST /api/users/{id}/deactivate (Admin only)
        // 2. GET /api/audit-logs (Admin only)
        // 3. GET /api/audit-logs/export (Admin only)
        // 4. GET /api/users/{Driver2Id} (Driver1 accessing Driver2 -> 403; Admin/Dispatcher -> 200)
        // 5. POST /api/users/{Driver2Id}/change-password (Driver1 modifying Driver2 -> 403; Dispatcher modifying Driver2 -> 403; Admin modifying Driver2 -> 204)

        // Matrix 断言 1~5: Admin 角色（5个端点均能访问）
        (await SendAuthorizedAsync(adminToken, HttpMethod.Post, $"/api/users/{_driver2Id}/deactivate")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendAuthorizedAsync(adminToken, HttpMethod.Get, "/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAuthorizedAsync(adminToken, HttpMethod.Get, "/api/audit-logs/export")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAuthorizedAsync(adminToken, HttpMethod.Get, $"/api/users/{_driver2Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAuthorizedAsync(adminToken, HttpMethod.Post, $"/api/users/{_driver2Id}/change-password", new ChangePasswordRequestBody(DefaultPassword, "NewAdminSetPass123!"))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Matrix 断言 6~10: Dispatcher 角色
        (await SendAuthorizedAsync(dispatcherToken, HttpMethod.Post, $"/api/users/{_driver2Id}/deactivate")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAuthorizedAsync(dispatcherToken, HttpMethod.Get, "/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAuthorizedAsync(dispatcherToken, HttpMethod.Get, "/api/audit-logs/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAuthorizedAsync(dispatcherToken, HttpMethod.Get, $"/api/users/{_dispatcherId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAuthorizedAsync(dispatcherToken, HttpMethod.Post, $"/api/users/{_driver2Id}/change-password", new ChangePasswordRequestBody(DefaultPassword, "FailPass123!"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Matrix 断言 11~15: Driver 角色（全部管理端点及越权端点均返回 403）
        (await SendAuthorizedAsync(driverToken, HttpMethod.Post, $"/api/users/{_driver2Id}/deactivate")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAuthorizedAsync(driverToken, HttpMethod.Get, "/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAuthorizedAsync(driverToken, HttpMethod.Get, "/api/audit-logs/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAuthorizedAsync(driverToken, HttpMethod.Get, $"/api/users/{_driver2Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAuthorizedAsync(driverToken, HttpMethod.Post, $"/api/users/{_driver2Id}/change-password", new ChangePasswordRequestBody(DefaultPassword, "FailPass123!"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task F1_5_AccountDeactivation_DeactivatedUserTokenFailsAuthenticationImmediately()
    {
        // 1. 司机登录拿到 Token
        var driverToken = await GetAccessTokenAsync(_driver1Email, DefaultPassword);

        // 验证司机当前可以正常调用自我资料接口
        var respBefore = await SendAuthorizedAsync(driverToken, HttpMethod.Get, $"/api/users/{_driver1Id}");
        respBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. 管理员停用司机
        var adminToken = await GetAccessTokenAsync(_adminEmail, DefaultPassword);
        var deactResp = await SendAuthorizedAsync(adminToken, HttpMethod.Post, $"/api/users/{_driver1Id}/deactivate");
        deactResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. 司机再次使用现有的 access token 请求，立即失效返回 401
        var respAfter = await SendAuthorizedAsync(driverToken, HttpMethod.Get, $"/api/users/{_driver1Id}");
        respAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task F1_6_PasswordPolicy_5FailedAttempts_LocksAccountFor15MinutesAndRecordsAudit()
    {
        for (var i = 0; i < 5; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(_driver2Email, "WrongPass!"))
            };
            req.Headers.Add("X-Forwarded-For", $"10.50.1.{i + 1}");
            var failResp = await _client.SendAsync(req);
            failResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // 第 6 次即便输入正确密码，由于被锁定依然返回 401 AUTH_LOCKED_OUT
        var checkReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(_driver2Email, DefaultPassword))
        };
        checkReq.Headers.Add("X-Forwarded-For", "10.50.1.99");
        var lockedResp = await _client.SendAsync(checkReq);
        lockedResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var lockedJson = await lockedResp.Content.ReadAsStringAsync();
        lockedJson.Should().Contain("AUTH_LOCKED_OUT");

        // 验证记录了锁定审计
        await using var context = _fixture.CreateDbContext();
        var audit = await context.AuditEvents.FirstOrDefaultAsync(a => a.Action == "User.Lockout" && a.EntityId == _driver2Id.ToString());
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task N1_1_AuditBehavior_RecordsAuditEventOnCommand()
    {
        var adminToken = await GetAccessTokenAsync(_adminEmail, DefaultPassword);

        // 执行修改密码命令（实现 IAuditableCommand）
        var resp = await SendAuthorizedAsync(adminToken, HttpMethod.Post, $"/api/users/{_adminId}/change-password",
            new ChangePasswordRequestBody(DefaultPassword, "NewAdminSuperPass123!"));
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var context = _fixture.CreateDbContext();
        var audit = await context.AuditEvents.FirstOrDefaultAsync(a => a.Action == "ChangePassword" && a.EntityId == _adminId.ToString());
        audit.Should().NotBeNull();
        audit!.ActorRole.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task N1_2_AuditEndpoints_GetAndExport_ReturnsLogs()
    {
        var adminToken = await GetAccessTokenAsync(_adminEmail, DefaultPassword);

        // 查询
        var getResp = await SendAuthorizedAsync(adminToken, HttpMethod.Get, "/api/audit-logs?page=1&pageSize=10");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await getResp.Content.ReadFromJsonAsync<PagedResult<AuditEventDto>>();
        paged.Should().NotBeNull();

        // 导出 CSV
        var exportResp = await SendAuthorizedAsync(adminToken, HttpMethod.Get, "/api/audit-logs/export");
        exportResp.StatusCode.Should().Be(HttpStatusCode.OK);
        exportResp.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await exportResp.Content.ReadAsStringAsync();
        csv.Should().StartWith("Id,OccurredAt,ActorUserId,ActorRole,Action,EntityType,EntityId,IpAddress,UserAgent,BeforeJson,AfterJson");
    }

    [Fact]
    public async Task N1_3_IDOR_Protection_DriverAccessingOtherDriverProfile_Returns403()
    {
        var driver1Token = await GetAccessTokenAsync(_driver1Email, DefaultPassword);

        // 司机1访问司机2的资料 -> 403 Forbidden
        var resp = await SendAuthorizedAsync(driver1Token, HttpMethod.Get, $"/api/users/{_driver2Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task N1_6_RateLimiting_MoreThan5RequestsPerMinute_Returns429WithRetryAfter()
    {
        const string testIp = "192.168.99.99";

        for (var i = 0; i < 5; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(_adminEmail, DefaultPassword))
            };
            req.Headers.Add("X-Forwarded-For", testIp);
            var r = await _client.SendAsync(req);
            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 第 6 次超限
        var limitReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(_adminEmail, DefaultPassword))
        };
        limitReq.Headers.Add("X-Forwarded-For", testIp);
        var rateLimitResp = await _client.SendAsync(limitReq);
        rateLimitResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rateLimitResp.Headers.Contains("Retry-After").Should().BeTrue();
    }

    private async Task<string> GetAccessTokenAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthSuccessResponse>();
        return body!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(string token, HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }
}

public class IdentityTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public IdentityTestWebApplicationFactory(string connectionString)
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
