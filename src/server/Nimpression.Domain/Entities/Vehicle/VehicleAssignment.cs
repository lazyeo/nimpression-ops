using Nimpression.Domain.Common;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Vehicle;

/// <summary>
/// 车辆指派记录实体，维护车辆在指定时段指派给哪位司机。
/// </summary>
public sealed class VehicleAssignment : Entity
{
    public Guid VehicleId { get; private set; }
    public Guid DriverId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public Guid AssignedByUserId { get; private set; }

    private VehicleAssignment()
    {
    }

    public VehicleAssignment(
        Guid id,
        Guid vehicleId,
        Guid driverId,
        DateTimeOffset assignedAt,
        Guid assignedByUserId) : base(id)
    {
        if (vehicleId == Guid.Empty)
        {
            throw new DomainValidationException("VehicleId cannot be empty.");
        }

        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("DriverId cannot be empty.");
        }

        if (assignedByUserId == Guid.Empty)
        {
            throw new DomainValidationException("AssignedByUserId cannot be empty.");
        }

        VehicleId = vehicleId;
        DriverId = driverId;
        AssignedAt = assignedAt;
        AssignedByUserId = assignedByUserId;
    }

    public bool IsActive => ReleasedAt == null;

    public void Release(DateTimeOffset releasedAt)
    {
        if (releasedAt < AssignedAt)
        {
            throw new DomainValidationException("Release time cannot be earlier than assignment time.");
        }

        ReleasedAt = releasedAt;
    }
}
