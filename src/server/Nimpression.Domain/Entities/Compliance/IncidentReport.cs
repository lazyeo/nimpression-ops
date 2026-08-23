using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Compliance;

/// <summary>
/// 事故报告聚合根。记录事故发生时间、地点、严重度、照片集合与加密的第三方信息。
/// </summary>
public sealed class IncidentReport : AggregateRoot
{
    private readonly List<string> _photoKeys = [];

    public Guid DriverId { get; private set; }
    public Guid VehicleId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public IncidentSeverity Severity { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? ThirdPartyInfoEnc { get; private set; }
    public string Status { get; private set; } = "Reported";
    public DateTimeOffset? InsurerNotifiedAt { get; private set; }

    public IReadOnlyList<string> PhotoKeys => _photoKeys.AsReadOnly();

    private IncidentReport()
    {
    }

    public IncidentReport(
        Guid id,
        Guid driverId,
        Guid vehicleId,
        DateTimeOffset occurredAt,
        string location,
        IncidentSeverity severity,
        string description,
        IEnumerable<string>? photoKeys = null,
        string? thirdPartyInfoEnc = null,
        string status = "Reported") : base(id)
    {
        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("DriverId cannot be empty.");
        }

        if (vehicleId == Guid.Empty)
        {
            throw new DomainValidationException("VehicleId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            throw new DomainValidationException("Location cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainValidationException("Description cannot be empty.");
        }

        DriverId = driverId;
        VehicleId = vehicleId;
        OccurredAt = occurredAt;
        Location = location.Trim();
        Severity = severity;
        Description = description.Trim();
        ThirdPartyInfoEnc = string.IsNullOrWhiteSpace(thirdPartyInfoEnc) ? null : thirdPartyInfoEnc.Trim();
        Status = string.IsNullOrWhiteSpace(status) ? "Reported" : status.Trim();

        if (photoKeys != null)
        {
            foreach (var key in photoKeys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _photoKeys.Add(key.Trim());
                }
            }
        }

        AddDomainEvent(new IncidentReported(Id, driverId, vehicleId, severity, occurredAt));
    }

    public void AddPhotoKey(string photoKey)
    {
        if (string.IsNullOrWhiteSpace(photoKey))
        {
            throw new DomainValidationException("Photo key cannot be empty.");
        }

        _photoKeys.Add(photoKey.Trim());
    }

    public void MarkInsurerNotified(DateTimeOffset notifiedAt)
    {
        InsurerNotifiedAt = notifiedAt;
    }

    public void UpdateStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new DomainValidationException("Status cannot be empty.");
        }

        Status = status.Trim();
    }

    public bool ShouldNotifyInsurer => Severity >= IncidentSeverity.Moderate;
}
