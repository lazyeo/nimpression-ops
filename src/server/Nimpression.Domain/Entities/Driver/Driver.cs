using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Driver;

/// <summary>
/// 司机聚合根。与 User 呈 1:1 关联，承载雇佣属性、混合制三档费率、加密联系信息与驾照合规状态。
/// </summary>
public sealed class Driver : AggregateRoot
{
    public Guid UserId { get; private set; }
    public string EmployeeNo { get; private set; } = string.Empty;
    public string LicenceClass { get; private set; } = string.Empty;
    public DateOnly LicenceExpiry { get; private set; }
    public Money HourlyRate { get; private set; }
    public Money PerTripRate { get; private set; }
    public Money PerKmRate { get; private set; }
    public string PhoneEnc { get; private set; } = string.Empty;
    public string AddressEnc { get; private set; } = string.Empty;
    public string EmergencyContactEnc { get; private set; } = string.Empty;
    public DateOnly HiredOn { get; private set; }
    public DriverStatus Status { get; private set; }

    private Driver()
    {
    }

    public Driver(
        Guid id,
        Guid userId,
        string employeeNo,
        string licenceClass,
        DateOnly licenceExpiry,
        Money hourlyRate,
        Money perTripRate,
        Money perKmRate,
        string phoneEnc,
        string addressEnc,
        string emergencyContactEnc,
        DateOnly hiredOn,
        DriverStatus status = DriverStatus.Active) : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("UserId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(employeeNo))
        {
            throw new DomainValidationException("Employee number cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(licenceClass))
        {
            throw new DomainValidationException("Licence class cannot be empty.");
        }

        UserId = userId;
        EmployeeNo = employeeNo.Trim().ToUpperInvariant();
        LicenceClass = licenceClass.Trim();
        LicenceExpiry = licenceExpiry;
        HourlyRate = hourlyRate;
        PerTripRate = perTripRate;
        PerKmRate = perKmRate;
        PhoneEnc = phoneEnc;
        AddressEnc = addressEnc;
        EmergencyContactEnc = emergencyContactEnc;
        HiredOn = hiredOn;
        Status = status;
    }

    public void UpdateRates(Money hourlyRate, Money perTripRate, Money perKmRate)
    {
        HourlyRate = hourlyRate;
        PerTripRate = perTripRate;
        PerKmRate = perKmRate;
    }

    public void UpdateEncryptedContactInfo(string phoneEnc, string addressEnc, string emergencyContactEnc)
    {
        PhoneEnc = phoneEnc;
        AddressEnc = addressEnc;
        EmergencyContactEnc = emergencyContactEnc;
    }

    public void UpdateLicence(string licenceClass, DateOnly licenceExpiry)
    {
        if (string.IsNullOrWhiteSpace(licenceClass))
        {
            throw new DomainValidationException("Licence class cannot be empty.");
        }

        LicenceClass = licenceClass.Trim();
        LicenceExpiry = licenceExpiry;
    }

    public void SetStatus(DriverStatus status)
    {
        Status = status;
    }

    public void Deactivate(DateTimeOffset deactivatedAt)
    {
        Status = DriverStatus.Inactive;
        AddDomainEvent(new DriverDeactivated(Id, UserId, deactivatedAt));
    }

    public bool IsLicenceExpired(DateOnly currentDate) => currentDate > LicenceExpiry;

    public bool CanBeDispatched(DateOnly currentDate)
    {
        return Status == DriverStatus.Active && !IsLicenceExpired(currentDate);
    }
}
