using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Entities;

public sealed class DriverTests
{
    [Fact]
    public void Driver_initializes_with_rates_and_guards()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var hourly = new Money(28.50m);
        var trip = new Money(45.00m);
        var km = new Money(0.65m);
        var expiry = new DateOnly(2027, 6, 30);
        var hired = new DateOnly(2025, 1, 15);

        var driver = new Driver(
            id,
            userId,
            "emp001",
            "Class 4",
            expiry,
            hourly,
            trip,
            km,
            "enc_phone",
            "enc_addr",
            "enc_contact",
            hired);

        Assert.Equal("EMP001", driver.EmployeeNo);
        Assert.Equal("Class 4", driver.LicenceClass);
        Assert.Equal(hourly, driver.HourlyRate);
        Assert.Equal(trip, driver.PerTripRate);
        Assert.Equal(km, driver.PerKmRate);
        Assert.Equal(DriverStatus.Active, driver.Status);
    }

    [Fact]
    public void Driver_throws_on_invalid_constructor_args()
    {
        var hourly = new Money(28.50m);
        var trip = new Money(45.00m);
        var km = new Money(0.65m);
        var expiry = new DateOnly(2027, 6, 30);
        var hired = new DateOnly(2025, 1, 15);

        Assert.Throws<DomainValidationException>(() => new Driver(
            Guid.NewGuid(), Guid.Empty, "EMP001", "Class 4", expiry, hourly, trip, km, "", "", "", hired));

        Assert.Throws<DomainValidationException>(() => new Driver(
            Guid.NewGuid(), Guid.NewGuid(), "  ", "Class 4", expiry, hourly, trip, km, "", "", "", hired));

        Assert.Throws<DomainValidationException>(() => new Driver(
            Guid.NewGuid(), Guid.NewGuid(), "EMP001", "", expiry, hourly, trip, km, "", "", "", hired));
    }

    [Fact]
    public void Driver_licence_expiry_and_dispatch_eligibility()
    {
        var expiry = new DateOnly(2026, 6, 30);
        var driver = new Driver(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "EMP002",
            "Class 2",
            expiry,
            new Money(26m),
            new Money(40m),
            new Money(0.50m),
            "p", "a", "e",
            new DateOnly(2024, 1, 1));

        Assert.False(driver.IsLicenceExpired(new DateOnly(2026, 6, 30)));
        Assert.True(driver.CanBeDispatched(new DateOnly(2026, 6, 30)));

        Assert.True(driver.IsLicenceExpired(new DateOnly(2026, 7, 1)));
        Assert.False(driver.CanBeDispatched(new DateOnly(2026, 7, 1)));

        driver.SetStatus(DriverStatus.OnLeave);
        Assert.False(driver.CanBeDispatched(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void Driver_deactivate_changes_status_and_emits_event()
    {
        var driver = new Driver(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "EMP003",
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(30m),
            new Money(50m),
            new Money(0.80m),
            "p", "a", "e",
            new DateOnly(2024, 1, 1));

        var deactivatedAt = DateTimeOffset.UtcNow;
        driver.Deactivate(deactivatedAt);

        Assert.Equal(DriverStatus.Inactive, driver.Status);
        var domainEvent = Assert.Single(driver.DomainEvents);
        var deactivatedEvent = Assert.IsType<DriverDeactivated>(domainEvent);
        Assert.Equal(driver.Id, deactivatedEvent.DriverId);
        Assert.Equal(driver.UserId, deactivatedEvent.UserId);
        Assert.Equal(deactivatedAt, deactivatedEvent.OccurredAt);
    }

    [Fact]
    public void Driver_updates_rates_and_contact_info()
    {
        var driver = new Driver(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "EMP004",
            "Class 2",
            new DateOnly(2028, 1, 1),
            new Money(25m),
            new Money(40m),
            new Money(0.50m),
            "p", "a", "e",
            new DateOnly(2024, 1, 1));

        driver.UpdateRates(new Money(30m), new Money(50m), new Money(0.75m));
        Assert.Equal(new Money(30m), driver.HourlyRate);
        Assert.Equal(new Money(50m), driver.PerTripRate);
        Assert.Equal(new Money(0.75m), driver.PerKmRate);

        driver.UpdateEncryptedContactInfo("new_p", "new_a", "new_e");
        Assert.Equal("new_p", driver.PhoneEnc);
        Assert.Equal("new_a", driver.AddressEnc);
        Assert.Equal("new_e", driver.EmergencyContactEnc);

        driver.UpdateLicence("Class 5", new DateOnly(2030, 1, 1));
        Assert.Equal("Class 5", driver.LicenceClass);
        Assert.Equal(new DateOnly(2030, 1, 1), driver.LicenceExpiry);
        Assert.Throws<DomainValidationException>(() => driver.UpdateLicence(" ", new DateOnly(2030, 1, 1)));
    }
}
