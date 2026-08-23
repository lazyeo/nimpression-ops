using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Persistence;

[Collection("PostgreSqlCollection")]
public class TimeZoneRoundtripTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public TimeZoneRoundtripTests(PostgreSqlContainerFixture fixture)
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
    public async Task Timestamptz_PreservesExactInstant_AcrossNewZealandDstTransitions()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();

        var user = new User(
            Guid.NewGuid(),
            new EmailAddress($"dst_user_{Guid.NewGuid():N}@nimpression.co.nz"),
            "hash",
            UserRole.Driver,
            "DST Driver",
            "en-NZ",
            DateTimeOffset.UtcNow);
        await context.Users.AddAsync(user);

        var driver = new Driver(
            Guid.NewGuid(),
            user.Id,
            $"DRV-{Guid.NewGuid():N}"[..7],
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "ENC(phone)",
            "ENC(addr)",
            "ENC(emg)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);
        await context.Drivers.AddAsync(driver);

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego($"D{Random.Shared.Next(10000, 99999)}"),
            "Isuzu",
            "NPR",
            2022,
            "ENC(VIN)",
            new Kilometres(10000),
            new Kilometres(10000),
            new Kilometres(0),
            null,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 1),
            VehicleStatus.Active);
        await context.Vehicles.AddAsync(vehicle);
        await context.SaveChangesAsync();

        // New Zealand 2026 DST transition:
        // 2026-04-05 03:00:00 (+13) shifts back to 02:00:00 (+12)
        var preTransitionTime = new DateTimeOffset(2026, 4, 4, 22, 0, 0, TimeSpan.FromHours(13)); // UTC 2026-04-04 09:00:00
        var postTransitionTime = new DateTimeOffset(2026, 4, 5, 6, 0, 0, TimeSpan.FromHours(12)); // UTC 2026-04-04 18:00:00

        var shift = new ShiftEntry(
            Guid.NewGuid(),
            driver.Id,
            preTransitionTime,
            -36.8485m,
            174.7633m,
            vehicle.Id);
        shift.ClockOut(postTransitionTime, -36.8485m, 174.7633m, 45, "DST night shift");

        await context.ShiftEntries.AddAsync(shift);
        await context.SaveChangesAsync();

        // Act: Read back from fresh DbContext to verify database roundtrip
        await using var readContext = _fixture.CreateDbContext();
        var reloadedShift = await readContext.ShiftEntries.FindAsync(shift.Id);

        // Assert: Exact instant equality preserved
        reloadedShift.Should().NotBeNull();
        reloadedShift!.ClockInAt.ToUniversalTime().Should().Be(preTransitionTime.ToUniversalTime());
        reloadedShift.ClockOutAt!.Value.ToUniversalTime().Should().Be(postTransitionTime.ToUniversalTime());

        // Net shift duration calculated in UTC must be exactly 8 hours minus 45 minutes = 7.25 hours
        var totalElapsed = reloadedShift.ClockOutAt!.Value - reloadedShift.ClockInAt;
        totalElapsed.TotalHours.Should().Be(9.0); // 9 clock hours elapsed due to fall-back
        var netHours = totalElapsed.TotalHours - (45.0 / 60.0);
        netHours.Should().Be(8.25);
    }
}
