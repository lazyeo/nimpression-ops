using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Vehicle;

/// <summary>
/// 车辆聚合根。维护车牌、车型、里程记录、保养间隔与合规到期日。
/// </summary>
public sealed class Vehicle : AggregateRoot
{
    public Rego Rego { get; private set; }
    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public string VinEnc { get; private set; } = string.Empty;
    public Kilometres OdometerKm { get; private set; }
    public Kilometres ServiceIntervalKm { get; private set; }
    public Kilometres LastServiceOdometerKm { get; private set; }
    public DateOnly? WofExpiry { get; private set; }
    public DateOnly? CofExpiry { get; private set; }
    public DateOnly? InsuranceExpiry { get; private set; }
    public VehicleStatus Status { get; private set; }

    private Vehicle()
    {
    }

    public Vehicle(
        Guid id,
        Rego rego,
        string make,
        string model,
        int year,
        string vinEnc,
        Kilometres odometerKm,
        Kilometres serviceIntervalKm,
        Kilometres? lastServiceOdometerKm = null,
        DateOnly? wofExpiry = null,
        DateOnly? cofExpiry = null,
        DateOnly? insuranceExpiry = null,
        VehicleStatus status = VehicleStatus.Active) : base(id)
    {
        if (string.IsNullOrWhiteSpace(make))
        {
            throw new DomainValidationException("Vehicle make cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new DomainValidationException("Vehicle model cannot be empty.");
        }

        if (year < 1900 || year > DateTime.UtcNow.Year + 2)
        {
            throw new DomainValidationException($"Invalid vehicle year: {year}.");
        }

        if (serviceIntervalKm.Value <= 0m)
        {
            throw new DomainValidationException("Service interval must be greater than zero.");
        }

        var lastService = lastServiceOdometerKm ?? Kilometres.Zero;
        if (lastService > odometerKm)
        {
            throw new DomainValidationException("Last service odometer cannot exceed current odometer.");
        }

        Rego = rego;
        Make = make.Trim();
        Model = model.Trim();
        Year = year;
        VinEnc = vinEnc;
        OdometerKm = odometerKm;
        ServiceIntervalKm = serviceIntervalKm;
        LastServiceOdometerKm = lastService;
        WofExpiry = wofExpiry;
        CofExpiry = cofExpiry;
        InsuranceExpiry = insuranceExpiry;
        Status = status;
    }

    public void UpdateOdometer(Kilometres newReading)
    {
        if (newReading < OdometerKm)
        {
            throw new DomainValidationException(
                $"New odometer reading ({newReading.Value} km) cannot be less than current reading ({OdometerKm.Value} km).");
        }

        OdometerKm = newReading;
    }

    public void RecordService(Kilometres serviceOdometer)
    {
        if (serviceOdometer < LastServiceOdometerKm)
        {
            throw new DomainValidationException(
                $"Service odometer ({serviceOdometer.Value} km) cannot be less than previous service ({LastServiceOdometerKm.Value} km).");
        }

        LastServiceOdometerKm = serviceOdometer;

        if (serviceOdometer > OdometerKm)
        {
            OdometerKm = serviceOdometer;
        }
    }

    public void UpdateComplianceDates(DateOnly? wofExpiry, DateOnly? cofExpiry, DateOnly? insuranceExpiry)
    {
        WofExpiry = wofExpiry;
        CofExpiry = cofExpiry;
        InsuranceExpiry = insuranceExpiry;
    }

    public void SetStatus(VehicleStatus status)
    {
        Status = status;
    }

    public Kilometres DistanceSinceLastService => OdometerKm - LastServiceOdometerKm;

    public bool IsServiceDue => DistanceSinceLastService >= ServiceIntervalKm;
}
