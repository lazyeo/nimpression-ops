using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Services;

public sealed class ServiceThresholdEvaluatorTests
{
    [Fact]
    public void ServiceThreshold_triggers_when_distance_reaches_interval()
    {
        var vehicleId = Guid.NewGuid();
        var currentOdo = new Kilometres(30500m);
        var lastServiceOdo = new Kilometres(20000m);
        var interval = new Kilometres(10000m);
        var evalTime = DateTimeOffset.UtcNow;

        var result = ServiceThresholdEvaluator.Evaluate(vehicleId, currentOdo, lastServiceOdo, interval, evalTime);

        Assert.True(result.NeedsService);
        Assert.Equal(3, result.ServiceCycleNo); // 30500 / 10000 = 3
        Assert.Equal($"SERVICE_{vehicleId:D}_3", result.IdempotencyKey);
        Assert.Equal(new Kilometres(500m), result.OverdueByKm);
        Assert.NotNull(result.DomainEvent);
        Assert.Equal(vehicleId, result.DomainEvent.VehicleId);
        Assert.Equal(3, result.DomainEvent.ServiceCycleNo);
        Assert.Equal(currentOdo, result.DomainEvent.CurrentOdometerKm);
        Assert.Equal(new Kilometres(30000m), result.DomainEvent.ThresholdKm);
        Assert.Equal(evalTime, result.DomainEvent.OccurredAt);
    }

    [Fact]
    public void ServiceThreshold_does_not_trigger_when_below_interval()
    {
        var vehicleId = Guid.NewGuid();
        var currentOdo = new Kilometres(28000m);
        var lastServiceOdo = new Kilometres(20000m);
        var interval = new Kilometres(10000m);

        var result = ServiceThresholdEvaluator.Evaluate(vehicleId, currentOdo, lastServiceOdo, interval);

        Assert.False(result.NeedsService);
        Assert.Equal(2, result.ServiceCycleNo);
        Assert.Equal(Kilometres.Zero, result.OverdueByKm);
        Assert.Null(result.DomainEvent);
    }

    [Fact]
    public void ServiceThreshold_same_cycle_produces_identical_idempotency_key()
    {
        var vehicleId = Guid.NewGuid();
        var lastServiceOdo = new Kilometres(20000m);
        var interval = new Kilometres(10000m);

        // Two odometer readings in the same cycle (e.g. 30100 and 30400)
        var res1 = ServiceThresholdEvaluator.Evaluate(vehicleId, new Kilometres(30100m), lastServiceOdo, interval);
        var res2 = ServiceThresholdEvaluator.Evaluate(vehicleId, new Kilometres(30400m), lastServiceOdo, interval);

        Assert.True(res1.NeedsService);
        Assert.True(res2.NeedsService);
        Assert.Equal(res1.IdempotencyKey, res2.IdempotencyKey);
        Assert.Equal(res1.ServiceCycleNo, res2.ServiceCycleNo);
    }

    [Fact]
    public void ServiceThreshold_evaluation_from_Vehicle_entity()
    {
        var vehicle = new Vehicle(
            Guid.NewGuid(),
            new Rego("TRK001"),
            "Scania",
            "R500",
            2023,
            "VIN123",
            new Kilometres(40000m),
            new Kilometres(15000m),
            new Kilometres(20000m)); // 20000km since last service >= 15000km

        var result = ServiceThresholdEvaluator.Evaluate(vehicle);

        Assert.True(result.NeedsService);
        Assert.Equal(2, result.ServiceCycleNo); // 40000 / 15000 = 2
        Assert.Equal(new Kilometres(5000m), result.OverdueByKm);
    }

    [Fact]
    public void ServiceThreshold_validation_guards()
    {
        Assert.Throws<DomainValidationException>(() =>
            ServiceThresholdEvaluator.Evaluate(Guid.Empty, new Kilometres(10000m), new Kilometres(5000m), new Kilometres(5000m)));

        Assert.Throws<DomainValidationException>(() =>
            ServiceThresholdEvaluator.Evaluate(Guid.NewGuid(), new Kilometres(10000m), new Kilometres(5000m), Kilometres.Zero));

        Assert.Throws<DomainValidationException>(() =>
            ServiceThresholdEvaluator.Evaluate(Guid.NewGuid(), new Kilometres(4000m), new Kilometres(5000m), new Kilometres(5000m)));
    }
}
