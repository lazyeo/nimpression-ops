using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Infrastructure.Persistence.Migrations;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Persistence;

[Collection("PostgreSqlCollection")]
public class MigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public MigrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var context = _fixture.CreateDbContext();
        await DatabaseMigrator.MigrateAsync(context);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migrate_Up_CreatesAllTablesAndAppliesSchema()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();

        // Act
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

        // Assert
        appliedMigrations.Should().Contain(m => m.Contains("InitialSchema"));

        // Verify that tables can be queried
        var userCount = await context.Users.CountAsync();
        var vehicleCount = await context.Vehicles.CountAsync();
        var areaCount = await context.Areas.CountAsync();

        userCount.Should().BeGreaterThanOrEqualTo(0);
        vehicleCount.Should().BeGreaterThanOrEqualTo(0);
        areaCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task DatabaseMigrator_ConcurrentExecution_SucceedsUnderAdvisoryLock()
    {
        // Arrange
        await using var context1 = _fixture.CreateDbContext();
        await using var context2 = _fixture.CreateDbContext();

        // Act: Execute two migrations concurrently to test advisory lock protection
        var task1 = DatabaseMigrator.MigrateAsync(context1);
        var task2 = DatabaseMigrator.MigrateAsync(context2);

        await Task.WhenAll(task1, task2);

        // Assert: Database schema remains valid and intact
        var appliedMigrations = await context1.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.Contains("InitialSchema"));
    }

    [Fact]
    public async Task Migrate_Down_RollsBackCleanly_AndCanReapplyUp()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();

        // Act: Rollback all migrations (Down to 0)
        await migrator.MigrateAsync("0");

        // Assert: No applied migrations
        var appliedAfterRollback = await context.Database.GetAppliedMigrationsAsync();
        appliedAfterRollback.Should().BeEmpty();

        // Act: Re-apply migration Up via DatabaseMigrator
        await DatabaseMigrator.MigrateAsync(context);

        // Assert: Migration reapplied successfully
        var appliedAfterReapply = await context.Database.GetAppliedMigrationsAsync();
        appliedAfterReapply.Should().Contain(m => m.Contains("InitialSchema"));
    }
}

