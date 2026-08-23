using Microsoft.EntityFrameworkCore;
using Nimpression.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nimpression.Integration.Tests.Fixtures;

public class PostgreSqlContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("nimpression_test")
        .WithUsername("nimpression")
        .WithPassword("devonly_change_me")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
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
