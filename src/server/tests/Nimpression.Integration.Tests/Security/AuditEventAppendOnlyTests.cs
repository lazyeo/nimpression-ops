using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;
using Nimpression.Integration.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace Nimpression.Integration.Tests.Security;

[Collection("PostgreSqlCollection")]
public class AuditEventAppendOnlyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public AuditEventAppendOnlyTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AuditEvent_Insert_Succeeds()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            "Driver.Created",
            "Driver",
            Guid.NewGuid().ToString(),
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            null,
            UserRole.Admin,
            null,
            "{\"status\":\"Active\"}",
            "127.0.0.1",
            "TestAgent/1.0");

        // Act
        await context.AuditEvents.AddAsync(auditEvent);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.AuditEvents.FindAsync(auditEvent.Id);
        saved.Should().NotBeNull();
        saved!.Action.Should().Be("Driver.Created");
    }

    [Fact]
    public async Task AuditEvent_Update_ThrowsExceptionDueToAppendOnlyConstraint()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            "Driver.Updated",
            "Driver",
            Guid.NewGuid().ToString(),
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            null,
            UserRole.Admin,
            "{\"old\":\"val\"}",
            "{\"new\":\"val\"}",
            "127.0.0.1",
            "TestAgent/1.0");

        await context.AuditEvents.AddAsync(auditEvent);
        await context.SaveChangesAsync();

        // Act: Attempting raw SQL or entity update on AuditEvent
        var act = async () =>
        {
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE \"AuditEvents\" SET \"Action\" = 'Tampered.Action' WHERE \"Id\" = {0}",
                auditEvent.Id);
        };

        // Assert: Database trigger or privilege revocation blocks UPDATE
        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.Message.Contains("append-only") || ex.SqlState == "42501" || ex.SqlState == "P0001");
    }

    [Fact]
    public async Task AuditEvent_Delete_ThrowsExceptionDueToAppendOnlyConstraint()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            "Driver.Deleted",
            "Driver",
            Guid.NewGuid().ToString(),
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            null,
            UserRole.Admin,
            null,
            null,
            "127.0.0.1",
            "TestAgent/1.0");

        await context.AuditEvents.AddAsync(auditEvent);
        await context.SaveChangesAsync();

        // Act: Attempting raw SQL or entity delete on AuditEvent
        var act = async () =>
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"AuditEvents\" WHERE \"Id\" = {0}",
                auditEvent.Id);
        };

        // Assert: Database trigger or privilege revocation blocks DELETE
        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.Message.Contains("append-only") || ex.SqlState == "42501" || ex.SqlState == "P0001");
    }
}
