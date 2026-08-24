using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nimpression.Application.Common.Security;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Realtime;

/// <summary>
/// F12.1 鉴权连接验收测试：
/// Hub 连接需携带有效 JWT；无效/过期/被停用用户的 token 直接拒绝握手（401 Unauthorized），绝非连上后再踢。
/// </summary>
[Collection("PostgreSqlCollection")]
public sealed class F12_1_AuthenticationAndHandshakeIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private RealtimeTestWebApplicationFactory _factory = null!;

    private readonly Guid _activeUserId = Guid.NewGuid();
    private readonly Guid _deactivatedUserId = Guid.NewGuid();
    private readonly string _activeUserEmail = TestDataFactory.CreateEmail("rt_auth_act");
    private readonly string _deactivatedUserEmail = TestDataFactory.CreateEmail("rt_auth_deact");

    public const string DefaultPassword = "SecurePassword123!";

    public F12_1_AuthenticationAndHandshakeIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new RealtimeTestWebApplicationFactory(_fixture.ConnectionString);

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var activeUser = new User(
            _activeUserId,
            new EmailAddress(_activeUserEmail),
            _passwordHasher.HashPassword(DefaultPassword),
            UserRole.Driver,
            "Active Driver User");

        var deactUser = new User(
            _deactivatedUserId,
            new EmailAddress(_deactivatedUserEmail),
            _passwordHasher.HashPassword(DefaultPassword),
            UserRole.Driver,
            "Deactivated Driver User");
        deactUser.SetStatus(UserStatus.Inactive);

        context.Users.AddRange(activeUser, deactUser);
        await context.SaveChangesAsync();
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
            var users = await context.Users
                .Where(u => u.Id == _activeUserId || u.Id == _deactivatedUserId)
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
    public async Task F12_1_Handshake_WithoutToken_DirectlyRejectsHandshakeWith401()
    {
        // Arrange: 无 Token 尝试建立连接
        await using var connection = _factory.CreateHubConnection(token: null);

        // Act
        var act = async () => await connection.StartAsync();

        // Assert: 握手阶段直接抛出 401 异常拒绝连接
        var exception = await Assert.ThrowsAnyAsync<Exception>(act);
        exception.Message.Should().MatchRegex("(401|Unauthorized|Response status code does not indicate success: 401)");
        connection.State.Should().Be(HubConnectionState.Disconnected);
    }

    [Fact]
    public async Task F12_1_Handshake_WithInvalidSignatureToken_DirectlyRejectsHandshakeWith401()
    {
        // Arrange: 使用伪造密钥签名的 Token
        var fakeJwtGenerator = new JwtTokenGenerator(Options.Create(new JwtSettings
        {
            Secret = "wrong_secret_key_that_is_at_least_32_bytes_long_123456",
            Issuer = "nimpression-api",
            Audience = "nimpression-client"
        }));

        var (invalidToken, _) = fakeJwtGenerator.GenerateAccessToken(_activeUserId, _activeUserEmail, "Driver", "Hacker");

        await using var connection = _factory.CreateHubConnection(token: invalidToken);

        // Act
        var act = async () => await connection.StartAsync();

        // Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(act);
        exception.Message.Should().MatchRegex("(401|Unauthorized|Response status code does not indicate success: 401)");
        connection.State.Should().Be(HubConnectionState.Disconnected);
    }

    [Fact]
    public async Task F12_1_Handshake_WithDeactivatedUserToken_DirectlyRejectsHandshakeWith401()
    {
        // Arrange: 账号已被停用的用户 Token
        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (deactToken, _) = jwtGenerator.GenerateAccessToken(_deactivatedUserId, _deactivatedUserEmail, "Driver", "Deactivated Driver");

        await using var connection = _factory.CreateHubConnection(token: deactToken);

        // Act
        var act = async () => await connection.StartAsync();

        // Assert: OnTokenValidated 校验停用状态直接中断握手
        var exception = await Assert.ThrowsAnyAsync<Exception>(act);
        exception.Message.Should().MatchRegex("(401|Unauthorized|Response status code does not indicate success: 401)");
        connection.State.Should().Be(HubConnectionState.Disconnected);
    }

    [Fact]
    public async Task F12_1_Handshake_WithValidToken_SuccessfullyConnects()
    {
        // Arrange: 活跃用户的合规 Token
        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (validToken, _) = jwtGenerator.GenerateAccessToken(_activeUserId, _activeUserEmail, "Driver", "Active Driver");

        await using var connection = _factory.CreateHubConnection(token: validToken);

        // Act
        await connection.StartAsync();

        // Assert
        connection.State.Should().Be(HubConnectionState.Connected);
        await connection.StopAsync();
    }
}
