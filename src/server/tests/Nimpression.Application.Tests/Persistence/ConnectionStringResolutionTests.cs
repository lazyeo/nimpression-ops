using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Nimpression.Infrastructure;
using Nimpression.Infrastructure.Persistence;
using Xunit;

namespace Nimpression.Application.Tests.Persistence;

public sealed class ConnectionStringResolutionTests : IDisposable
{
    private readonly string? _origConnStrEnv;
    private readonly string? _origConnStrShortEnv;
    private readonly string? _origDatabaseUrl;

    public ConnectionStringResolutionTests()
    {
        _origConnStrEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        _origConnStrShortEnv = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        _origDatabaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        Environment.SetEnvironmentVariable("DATABASE_URL", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _origConnStrEnv);
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _origConnStrShortEnv);
        Environment.SetEnvironmentVariable("DATABASE_URL", _origDatabaseUrl);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ResolveConnectionString_WhenConfigurationAndEnvMissing_ThrowsInvalidOperationException_WithKeyNames()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        // Act
        var act = () => Nimpression.Infrastructure.DependencyInjection.ResolveConnectionString(config);

        // Assert
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("ConnectionStrings__DefaultConnection");
        ex.Message.Should().Contain("DATABASE_URL");
        ex.Message.Should().Contain("Database connection string is missing");
    }

    [Fact]
    public void ResolveConnectionString_WhenConfiguredViaGetConnectionString_ReturnsConfiguredString()
    {
        // Arrange
        const string expected = "Host=my-prod-db;Port=5432;Database=nimpression;Username=app;Password=secret"; // allow-hardcoded: unit test connection string assertion
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = expected
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var result = Nimpression.Infrastructure.DependencyInjection.ResolveConnectionString(config);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ResolveConnectionString_WhenConfiguredViaEnvironmentVariable_ReturnsConfiguredString()
    {
        // Arrange
        const string expected = "Host=my-env-db;Port=5432;Database=nimpression;Username=app;Password=secret"; // allow-hardcoded: unit test connection string assertion
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", expected);
        var config = new ConfigurationBuilder().Build();

        // Act
        var result = Nimpression.Infrastructure.DependencyInjection.ResolveConnectionString(config);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ResolveConnectionString_WhenConfiguredViaDatabaseUrl_ReturnsConfiguredString()
    {
        // Arrange
        const string expected = "Host=my-database-url-db;Port=5432;Database=nimpression;Username=app;Password=secret"; // allow-hardcoded: unit test connection string assertion
        Environment.SetEnvironmentVariable("DATABASE_URL", expected);
        var config = new ConfigurationBuilder().Build();

        // Act
        var result = Nimpression.Infrastructure.DependencyInjection.ResolveConnectionString(config);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void AppDbContextFactory_CreateDbContext_WhenConnectionStringMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new AppDbContextFactory();

        // Act
        var act = () => factory.CreateDbContext([]);

        // Assert
        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("ConnectionStrings__DefaultConnection");
        ex.Message.Should().Contain("Design-time DbContext creation requires a valid database connection string");
    }

    [Fact]
    public void AppDbContextFactory_CreateDbContext_WhenConnectionStringProvided_ReturnsAppDbContext()
    {
        // Arrange
        const string connStr = "Host=localhost;Port=5432;Database=test_design_time;Username=user;Password=pass"; // allow-hardcoded: unit test design time connection string
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connStr);
        var factory = new AppDbContextFactory();

        // Act
        using var context = factory.CreateDbContext([]);

        // Assert
        context.Should().NotBeNull();
    }
}
