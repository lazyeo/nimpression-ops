using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Api.Endpoints;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Vehicles.Commands.CreateVehicle;
using Nimpression.Application.Features.Vehicles.Commands.RecordOdometerReading;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Security;

/// <summary>
/// 全局端点授权策略防回归测试套件。
/// <para>
/// 确保所有 Minimal API 端点模块均显式挂载授权策略（<see cref="AuthorizationPolicies"/>），
/// 杜绝未经授权裸奔的端点遗漏进入生产环境。
/// </para>
/// </summary>
[Collection("PostgreSqlCollection")]
public sealed class EndpointAuthorizationRegressionTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private EndpointAuthTestAppFactory _factory = null!;
    private HttpClient _client = null!;
    private IJwtTokenGenerator _tokenGenerator = null!;

    public EndpointAuthorizationRegressionTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new EndpointAuthTestAppFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        _tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// 显式匿名端点白名单字典：[HTTP Method + Route Pattern] -> 豁免原因。
    /// 任何未在此白名单中的端点必须附带 <see cref="IAuthorizeData"/> 元数据。
    /// </summary>
    private static readonly Dictionary<string, string> AnonymousEndpointAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET /health"] =
            "系统健康检查探针，供 Docker / Kubernetes 存活探针及负载均衡器无鉴权轮询。",

        ["POST /api/auth/login"] =
            "用户身份认证登录入口，接收凭据签发 JWT 与 Refresh Token。受 IP 限流保护（5次/分/IP）。",

        ["POST /api/auth/refresh"] =
            "令牌轮转刷新入口，通过 HttpOnly Cookie 或 Header 中的 Refresh Token 交换新 Access Token。",

        ["POST /api/auth/logout"] =
            "用户注销入口，使当前 Refresh Token 失效并清除 Cookie，支持未认证或过期会话发起退出。",

        ["GET /openapi/{documentName}.json"] =
            "OpenAPI / Swagger 文档契约定义，供前端代码生成与联调工具使用（在开发环境启用）。",

        ["GET /openapi/v1.json"] =
            "OpenAPI v1 文档契约定义。"
    };

    [Fact]
    public void AllMinimalApiEndpoints_MustHaveExplicitAuthorization_UnlessExplicitlyAllowlisted()
    {
        // Arrange
        var endpointDataSources = _factory.Services.GetServices<EndpointDataSource>();
        var allEndpoints = endpointDataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        allEndpoints.Should().NotBeEmpty("At least one RouteEndpoint should be registered in the application.");

        var unprotectedEndpoints = new List<string>();

        // Act
        foreach (var endpoint in allEndpoints)
        {
            var routePattern = "/" + endpoint.RoutePattern.RawText?.TrimStart('/');
            var httpMethods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"];
            var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();

            var isAuthorized = authorizeData != null && authorizeData.Count > 0;

            foreach (var method in httpMethods)
            {
                var key = $"{method} {routePattern}";

                if (!isAuthorized)
                {
                    // 检查是否在显式白名单中
                    var isAllowlisted = AnonymousEndpointAllowlist.ContainsKey(key) ||
                                        AnonymousEndpointAllowlist.Keys.Any(k =>
                                            k.StartsWith(method + " ", StringComparison.OrdinalIgnoreCase) &&
                                            (routePattern.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) ||
                                             key.Equals(k, StringComparison.OrdinalIgnoreCase)));

                    if (!isAllowlisted)
                    {
                        var displayName = endpoint.DisplayName ?? routePattern;
                        unprotectedEndpoints.Add($"[{method}] {routePattern} (DisplayName: '{displayName}')");
                    }
                }
            }
        }

        // Assert
        unprotectedEndpoints.Should().BeEmpty(
            "All endpoints must be protected by RequireAuthorization() or explicitly documented in the AnonymousEndpointAllowlist. " +
            "Found unprotected endpoint(s):\n" + string.Join("\n", unprotectedEndpoints));
    }

    [Fact]
    public void EndpointModuleSourceFiles_MustNotOmitRequireAuthorization()
    {
        // Arrange: 扫描 Api/Endpoints 目录下所有 C# 源码文件
        var apiAssemblyDir = AppContext.BaseDirectory;
        var solutionDir = Directory.GetParent(apiAssemblyDir);
        while (solutionDir != null && !File.Exists(Path.Combine(solutionDir.FullName, "Taskfile.yml")))
        {
            solutionDir = solutionDir.Parent;
        }

        solutionDir.Should().NotBeNull("Solution directory must be resolved.");

        var endpointsDir = Path.Combine(solutionDir!.FullName, "src", "server", "Nimpression.Api", "Endpoints");
        Directory.Exists(endpointsDir).Should().BeTrue($"Endpoints directory should exist at '{endpointsDir}'");

        var endpointFiles = Directory.GetFiles(endpointsDir, "*Endpoints.cs", SearchOption.TopDirectoryOnly);
        endpointFiles.Should().NotBeEmpty();

        var missingAuthFiles = new List<string>();

        // Act
        foreach (var file in endpointFiles)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);

            if (!content.Contains("IEndpointModule"))
            {
                continue;
            }

            var hasRequireAuth = content.Contains(".RequireAuthorization(");
            if (!hasRequireAuth)
            {
                missingAuthFiles.Add(fileName);
            }
        }

        // Assert
        missingAuthFiles.Should().BeEmpty(
            "Every IEndpointModule file in Api/Endpoints must contain at least one RequireAuthorization call. " +
            "Offending file(s): " + string.Join(", ", missingAuthFiles));
    }

    [Fact]
    public async Task VehiclesApi_DriverToken_AttemptingGetVehicles_Returns403Forbidden()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var driverUser = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver_get"),
            "hash",
            UserRole.Driver,
            "Test Driver",
            "en-NZ");
        await context.Users.AddAsync(driverUser);
        await context.SaveChangesAsync();

        var (driverToken, _) = _tokenGenerator.GenerateAccessToken(driverUser.Id, driverUser.Email.Value, UserRole.Driver.ToString(), "Test Driver");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/vehicles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Drivers cannot access management vehicle list");
    }

    [Fact]
    public async Task VehiclesApi_DispatcherToken_CanAccessVehiclesList_Returns200OK()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var dispatcherUser = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("dispatcher_get"),
            "hash",
            UserRole.Dispatcher,
            "Fleet Dispatcher",
            "en-NZ");
        await context.Users.AddAsync(dispatcherUser);
        await context.SaveChangesAsync();

        var (dispatcherToken, _) = _tokenGenerator.GenerateAccessToken(dispatcherUser.Id, dispatcherUser.Email.Value, UserRole.Dispatcher.ToString(), "Fleet Dispatcher");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/vehicles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dispatcherToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Dispatchers are authorized to query vehicle list");
    }

    [Fact]
    public async Task VehiclesApi_DispatcherToken_AttemptingCreateVehicle_Returns403Forbidden()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var dispatcherUser = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("dispatcher_create"),
            "hash",
            UserRole.Dispatcher,
            "Fleet Dispatcher",
            "en-NZ");
        await context.Users.AddAsync(dispatcherUser);
        await context.SaveChangesAsync();

        var (dispatcherToken, _) = _tokenGenerator.GenerateAccessToken(dispatcherUser.Id, dispatcherUser.Email.Value, UserRole.Dispatcher.ToString(), "Fleet Dispatcher");

        var createRequest = new CreateVehicleRequest(
            TestDataFactory.CreateRego("V"),
            "Toyota",
            "Hilux",
            2023,
            "ENC(VIN_TEST)",
            10000m,
            10000m);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles")
        {
            Content = JsonContent.Create(createRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dispatcherToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Only Admins can create new vehicles");
    }

    [Fact]
    public async Task VehiclesApi_DriverSubmittingOdometerForUnassignedVehicle_Returns403Forbidden()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();

        var driverUser = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver_odo"),
            "hash",
            UserRole.Driver,
            "Odo Driver",
            "en-NZ");
        await context.Users.AddAsync(driverUser);

        var driver = new Driver(
            Guid.NewGuid(),
            driverUser.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 2",
            new DateOnly(2028, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "ENC(021000)",
            "ENC(Auckland)",
            "ENC(Emergency)",
            new DateOnly(2024, 1, 1));
        await context.Drivers.AddAsync(driver);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            TestDataFactory.CreateRegoObject("ODO"),
            "Isuzu",
            "Elf",
            2022,
            "ENC(VIN)",
            new Kilometres(20000),
            new Kilometres(10000));
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        var (driverToken, _) = _tokenGenerator.GenerateAccessToken(driverUser.Id, driverUser.Email.Value, UserRole.Driver.ToString(), driverUser.DisplayName);

        var odoRequest = new RecordOdometerReadingRequest(
            driver.Id,
            21000m);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/vehicles/{vehicle.Id}/odometer")
        {
            Content = JsonContent.Create(odoRequest)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Drivers cannot record odometer readings for unassigned vehicles");
    }
}

public sealed class EndpointAuthTestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public EndpointAuthTestAppFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.ConfigureServices(services =>
        {
            var hostedServices = services.Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)).ToList();
            foreach (var hs in hostedServices)
            {
                services.Remove(hs);
            }
        });
    }
}
