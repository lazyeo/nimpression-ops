using System.Net;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Startup;

/// <summary>
/// 真实应用引导与全量依赖注入验证测试（W7b / AC N3.4）。
/// 验证应用能以完整 DI 容器成功启动，且所有 MediatR Handlers 及 Application 抽象仓储均可正确解析，
/// 杜绝缺失 DI 注册（如 IVehicleRepository 遗漏）导致的运行时崩溃。
/// </summary>
[Collection("PostgreSqlCollection")]
public sealed class ApplicationStartupTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private ApplicationStartupTestAppFactory _factory = null!;
    private HttpClient _client = null!;

    public ApplicationStartupTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new ApplicationStartupTestAppFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task Application_Host_Starts_Successfully_And_Responds_To_HealthCheck()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }

    [Fact]
    public async Task HealthCheck_WhenDatabaseIsUnmigrated_ReturnsServiceUnavailable_WithDiagnostics()
    {
        var dbName = $"unmigrated_{Guid.NewGuid():N}";
        await using (var adminContext = _fixture.CreateDbContext())
        {
            var conn = adminContext.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{dbName}\";";
            await cmd.ExecuteNonQueryAsync();
            await conn.CloseAsync();
        }

        try
        {
            var unmigratedConnStr = new Npgsql.NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
            {
                Database = dbName
            }.ConnectionString;

            using var unmigratedFactory = new ApplicationStartupTestAppFactory(unmigratedConnStr);
            using var client = unmigratedFactory.CreateClient();

            var response = await client.GetAsync("/health");

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("unhealthy");
            content.Should().Contain("pending migration");
        }
        finally
        {
            await using var adminContext = _fixture.CreateDbContext();
            var conn = adminContext.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE);";
            await cmd.ExecuteNonQueryAsync();
            await conn.CloseAsync();
        }
    }

    [Fact]
    public void SeedConstants_DefaultPasswordHash_Matches_DefaultPassword()
    {
        var hasher = new Nimpression.Infrastructure.Security.PasswordHasher();
        hasher.VerifyPassword(Nimpression.Infrastructure.Persistence.Seed.SeedConstants.DefaultPassword,
            Nimpression.Infrastructure.Persistence.Seed.SeedConstants.DefaultPasswordHash).Should().BeTrue();
    }

    [Fact]
    public void All_Application_Repository_Interfaces_Are_Registered_And_Resolvable()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var appAssembly = typeof(ApplicationAssemblyMarker).Assembly;
        var repositoryInterfaces = appAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToList();

        repositoryInterfaces.Should().NotBeEmpty();

        var missingRegistrations = new List<string>();

        foreach (var repoInterface in repositoryInterfaces)
        {
            try
            {
                var resolved = sp.GetService(repoInterface);
                if (resolved is null)
                {
                    missingRegistrations.Add($"Interface {repoInterface.FullName} is not registered in DI.");
                }
            }
            catch (Exception ex)
            {
                missingRegistrations.Add($"Interface {repoInterface.FullName} failed to resolve: {ex.Message}");
            }
        }

        missingRegistrations.Should().BeEmpty(
            "All Application repository interfaces must be registered and resolvable from DI container");
    }

    [Fact]
    public void All_MediatR_Request_Handlers_Can_Be_Resolved_From_DI_Container()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var appAssembly = typeof(ApplicationAssemblyMarker).Assembly;
        var handlerTypes = appAssembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
                 i.GetGenericTypeDefinition() == typeof(IRequestHandler<>))))
            .ToList();

        handlerTypes.Should().NotBeEmpty();

        var resolutionFailures = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            try
            {
                var handlerInterfaces = handlerType.GetInterfaces()
                    .Where(i => i.IsGenericType &&
                        (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
                         i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)))
                    .ToList();

                foreach (var handlerInterface in handlerInterfaces)
                {
                    var resolved = sp.GetService(handlerInterface);
                    if (resolved is null)
                    {
                        resolutionFailures.Add($"Handler {handlerType.Name} implementing {handlerInterface} could not be resolved.");
                    }
                }
            }
            catch (Exception ex)
            {
                resolutionFailures.Add($"Handler {handlerType.FullName} failed with error: {ex.Message}");
            }
        }

        resolutionFailures.Should().BeEmpty(
            "All MediatR handlers must have all constructor dependencies registered and resolvable in DI");
    }
}

public sealed class ApplicationStartupTestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ApplicationStartupTestAppFactory(string connectionString)
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
