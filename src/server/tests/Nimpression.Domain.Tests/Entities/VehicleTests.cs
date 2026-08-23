using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Entities;

public sealed class VehicleTests
{
    [Fact]
    public void Vehicle_initializes_with_valid_attributes()
    {
        var id = Guid.NewGuid();
        var rego = new Rego("NIM001");
        var odo = new Kilometres(12000m);
        var interval = new Kilometres(10000m);
        var lastService = new Kilometres(10000m);

        var vehicle = new Vehicle(
            id,
            rego,
            "Isuzu",
            "NPR",
            2022,
            "ENC_VIN_123",
            odo,
            interval,
            lastService);

        Assert.Equal(rego, vehicle.Rego);
        Assert.Equal("Isuzu", vehicle.Make);
        Assert.Equal("NPR", vehicle.Model);
        Assert.Equal(2022, vehicle.Year);
        Assert.Equal(odo, vehicle.OdometerKm);
        Assert.Equal(interval, vehicle.ServiceIntervalKm);
        Assert.Equal(lastService, vehicle.LastServiceOdometerKm);
        Assert.Equal(new Kilometres(2000m), vehicle.DistanceSinceLastService);
        Assert.False(vehicle.IsServiceDue);
    }

    [Fact]
    public void Vehicle_throws_on_invalid_constructor_args()
    {
        var rego = new Rego("NIM001");
        var odo = new Kilometres(5000m);
        var interval = new Kilometres(10000m);

        Assert.Throws<DomainValidationException>(() => new Vehicle(
            Guid.NewGuid(), rego, "", "Model", 2022, "VIN", odo, interval));

        Assert.Throws<DomainValidationException>(() => new Vehicle(
            Guid.NewGuid(), rego, "Make", "", 2022, "VIN", odo, interval));

        Assert.Throws<DomainValidationException>(() => new Vehicle(
            Guid.NewGuid(), rego, "Make", "Model", 1899, "VIN", odo, interval));

        Assert.Throws<DomainValidationException>(() => new Vehicle(
            Guid.NewGuid(), rego, "Make", "Model", 2022, "VIN", odo, Kilometres.Zero));

        Assert.Throws<DomainValidationException>(() => new Vehicle(
            Guid.NewGuid(), rego, "Make", "Model", 2022, "VIN", odo, interval,
            lastServiceOdometerKm: new Kilometres(6000m)));
    }

    [Fact]
    public void Vehicle_odometer_update_and_monotonic_guard()
    {
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC111"),
            "Toyota",
            "Dyna",
            2021,
            "VIN",
            new Kilometres(15000m),
            new Kilometres(10000m),
            new Kilometres(10000m));

        vehicle.UpdateOdometer(new Kilometres(16000m));
        Assert.Equal(new Kilometres(16000m), vehicle.OdometerKm);
        Assert.Equal(new Kilometres(6000m), vehicle.DistanceSinceLastService);
        Assert.False(vehicle.IsServiceDue);

        vehicle.UpdateOdometer(new Kilometres(20000m));
        Assert.Equal(new Kilometres(10000m), vehicle.DistanceSinceLastService);
        Assert.True(vehicle.IsServiceDue);

        // Cannot decrease odometer
        Assert.Throws<DomainValidationException>(() => vehicle.UpdateOdometer(new Kilometres(19999m)));
    }

    [Fact]
    public void Vehicle_record_service_resets_cycle()
    {
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("ABC111"),
            "Toyota",
            "Dyna",
            2021,
            "VIN",
            new Kilometres(20500m),
            new Kilometres(10000m),
            new Kilometres(10000m));

        Assert.True(vehicle.IsServiceDue);

        vehicle.RecordService(new Kilometres(20500m));
        Assert.Equal(new Kilometres(20500m), vehicle.LastServiceOdometerKm);
        Assert.Equal(new Kilometres(0m), vehicle.DistanceSinceLastService);
        Assert.False(vehicle.IsServiceDue);

        Assert.Throws<DomainValidationException>(() => vehicle.RecordService(new Kilometres(20000m)));
    }

    [Fact]
    public void VehicleAssignment_and_OdometerReading_behaviors()
    {
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var assignment = new VehicleAssignment(Guid.NewGuid(), vehicleId, driverId, now, adminId);
        Assert.True(assignment.IsActive);

        assignment.Release(now.AddHours(4));
        Assert.False(assignment.IsActive);
        Assert.Equal(now.AddHours(4), assignment.ReleasedAt);

        Assert.Throws<DomainValidationException>(() => assignment.Release(now.AddHours(-1)));

        var reading = new OdometerReading(
            Guid.NewGuid(), vehicleId, driverId, new Kilometres(12345m), "photo.jpg", now, "DriverApp");
        Assert.Equal(new Kilometres(12345m), reading.ReadingKm);
        Assert.Equal("photo.jpg", reading.PhotoKey);
        Assert.Equal("DriverApp", reading.Source);
    }
}
