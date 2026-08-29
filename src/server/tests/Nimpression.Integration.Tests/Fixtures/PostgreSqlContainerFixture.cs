using Microsoft.EntityFrameworkCore;
using Nimpression.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nimpression.Integration.Tests.Fixtures;

public class PostgreSqlContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("nimpression_test")
        .WithUsername("nimpression")
        .WithPassword("devonly_change_me")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENCRYPTION_KEY")))
        {
            Environment.SetEnvironmentVariable("ENCRYPTION_KEY", "k8+1h7T7mK6rL4p5v3z9Q1w2e3r4t5y6u7i8o9p0a1s=");
        }
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}

#pragma warning disable CA1711
[CollectionDefinition("PostgreSqlCollection")]
public class PostgreSqlTestCollectionDefinition : ICollectionFixture<PostgreSqlContainerFixture>
{
}
#pragma warning restore CA1711
