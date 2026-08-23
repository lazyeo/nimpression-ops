using Nimpression.Domain.Common;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Area;

/// <summary>
/// 司机区域分配实体，支持生效期约束与重叠判定。
/// </summary>
public sealed class AreaAssignment : Entity
{
    public Guid AreaId { get; private set; }
    public Guid DriverId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }

    private AreaAssignment()
    {
    }

    public AreaAssignment(
        Guid id,
        Guid areaId,
        Guid driverId,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null) : base(id)
    {
        if (areaId == Guid.Empty)
        {
            throw new DomainValidationException("AreaId cannot be empty.");
        }

        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("DriverId cannot be empty.");
        }

        if (effectiveTo.HasValue && effectiveTo.Value < effectiveFrom)
        {
            throw new DomainValidationException(
                $"EffectiveTo ({effectiveTo.Value:yyyy-MM-dd}) cannot be earlier than EffectiveFrom ({effectiveFrom:yyyy-MM-dd}).");
        }

        AreaId = areaId;
        DriverId = driverId;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public bool IsEffectiveOn(DateOnly date)
    {
        return date >= EffectiveFrom && (!EffectiveTo.HasValue || date <= EffectiveTo.Value);
    }

    public bool OverlapsWith(DateOnly from, DateOnly? to)
    {
        var otherEnd = to ?? DateOnly.MaxValue;
        var thisEnd = EffectiveTo ?? DateOnly.MaxValue;

        return EffectiveFrom <= otherEnd && from <= thisEnd;
    }

    public void EndAssignment(DateOnly effectiveTo)
    {
        if (effectiveTo < EffectiveFrom)
        {
            throw new DomainValidationException(
                $"EffectiveTo ({effectiveTo:yyyy-MM-dd}) cannot be earlier than EffectiveFrom ({EffectiveFrom:yyyy-MM-dd}).");
        }

        EffectiveTo = effectiveTo;
    }
}
