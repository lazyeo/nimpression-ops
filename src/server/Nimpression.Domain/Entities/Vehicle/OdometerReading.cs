using Nimpression.Domain.Common;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Vehicle;

/// <summary>
/// 车辆里程读数实体。不可就地覆盖，保留历史轨迹与照片凭证供保养审计。
/// </summary>
public sealed class OdometerReading : Entity
{
    public Guid VehicleId { get; private set; }
    public Guid DriverId { get; private set; }
    public Kilometres ReadingKm { get; private set; }
    public string? PhotoKey { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public string Source { get; private set; } = "DriverApp";

    private OdometerReading()
    {
    }

    public OdometerReading(
        Guid id,
        Guid vehicleId,
        Guid driverId,
        Kilometres readingKm,
        string? photoKey,
        DateTimeOffset recordedAt,
        string source = "DriverApp") : base(id)
    {
        if (vehicleId == Guid.Empty)
        {
            throw new DomainValidationException("VehicleId cannot be empty.");
        }

        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("DriverId cannot be empty.");
        }

        VehicleId = vehicleId;
        DriverId = driverId;
        ReadingKm = readingKm;
        PhotoKey = string.IsNullOrWhiteSpace(photoKey) ? null : photoKey.Trim();
        RecordedAt = recordedAt;
        Source = string.IsNullOrWhiteSpace(source) ? "DriverApp" : source.Trim();
    }
}
