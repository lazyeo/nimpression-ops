using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.ValueObjects;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.TransactionalOutbox;

[Collection("PostgreSqlCollection")]
public class OutboxTransactionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public OutboxTransactionTests(PostgreSqlContainerFixture fixture)
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
    public async Task SaveChangesAsync_CapturesDomainEventsIntoOutbox_InSameTransaction()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();

        var user = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("outbox_user"),
            "hash",
            UserRole.Driver,
            "Outbox Driver",
            "en-NZ",
            DateTimeOffset.UtcNow);
        await context.Users.AddAsync(user);

        var driver = new Driver(
            Guid.NewGuid(),
            user.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(32m),
            new Money(45m),
            new Money(0.85m),
            "ENC(phone)",
            "ENC(addr)",
            "ENC(emg)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);
        await context.Drivers.AddAsync(driver);
        await context.SaveChangesAsync();

        // Clear outbox from creation events
        var initialOutboxCount = await context.OutboxMessages.CountAsync();

        // Act: Trigger a domain event on aggregate root
        driver.Deactivate(DateTimeOffset.UtcNow);
        driver.DomainEvents.Should().ContainSingle(e => e is DriverDeactivated);

        var savedCount = await context.SaveChangesAsync();

        // Assert: Aggregate root domain events are cleared and written into OutboxMessages table
        driver.DomainEvents.Should().BeEmpty();

        var outboxEntries = await context.OutboxMessages
            .Where(m => m.Type == nameof(DriverDeactivated))
            .ToListAsync();

        outboxEntries.Should().NotBeEmpty();
        var latest = outboxEntries.OrderByDescending(m => m.OccurredAt).First();
        latest.PayloadJson.Should().Contain(driver.Id.ToString());
        latest.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task TransactionRollback_RevertsBothEntityAndOutboxMessage()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();

        var user = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("rollback_user"),
            "hash",
            UserRole.Driver,
            "Rollback Driver",
            "en-NZ",
            DateTimeOffset.UtcNow);
        await context.Users.AddAsync(user);

        var driver = new Driver(
            Guid.NewGuid(),
            user.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(32m),
            new Money(45m),
            new Money(0.85m),
            "ENC(phone)",
            "ENC(addr)",
            "ENC(emg)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);
        await context.Drivers.AddAsync(driver);
        await context.SaveChangesAsync();

        // Act: Open explicit transaction, deactivate driver, save changes, then ROLLBACK
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            driver.Deactivate(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();

            // Rollback transaction
            await transaction.RollbackAsync();
        }

        // Assert: Reload in fresh DbContext to verify atomic rollback
        await using var verifyContext = _fixture.CreateDbContext();
        var reloadedDriver = await verifyContext.Drivers.FindAsync(driver.Id);
        reloadedDriver.Should().NotBeNull();
        reloadedDriver!.Status.Should().Be(DriverStatus.Active); // Not Deactivated!

        var outboxCount = await verifyContext.OutboxMessages
            .Where(m => m.Type == nameof(DriverDeactivated) && m.PayloadJson.Contains(driver.Id.ToString()))
            .CountAsync();

        outboxCount.Should().Be(0); // Outbox message rolled back atomically with entity!
    }
}
