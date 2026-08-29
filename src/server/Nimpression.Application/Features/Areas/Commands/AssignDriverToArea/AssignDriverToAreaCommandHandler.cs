using System.Globalization;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Areas.Commands.AssignDriverToArea;

/// <summary>
/// 为司机分配区域命令处理器。
/// F4.2: 同一司机同一区域的生效期不可重叠，重叠时 422 并指出冲突区间。
/// 使用 Domain 层的 DateRange.Overlaps() 判定。
/// </summary>
public sealed class AssignDriverToAreaCommandHandler(
    IAreaRepository areaRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<AssignDriverToAreaCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AssignDriverToAreaCommand request, CancellationToken cancellationToken)
    {
        var area = await areaRepository.GetByIdAsync(request.AreaId, cancellationToken);
        if (area is null)
        {
            return Error.NotFound("area_not_found", $"Area with ID '{request.AreaId}' was not found.");
        }

        var driverExists = await areaRepository.DriverExistsAsync(request.DriverId, cancellationToken);
        if (!driverExists)
        {
            return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId}' was not found.");
        }

        DateRange newRange;
        try
        {
            newRange = new DateRange(request.EffectiveFrom, request.EffectiveTo ?? DateOnly.MaxValue);
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_date_range", ex.Message);
        }

        // 查询该司机在该区域的所有已有分配
        var existingAssignments = await areaRepository.GetAssignmentsForDriverAndAreaAsync(
            request.DriverId,
            request.AreaId,
            cancellationToken);

        foreach (var existing in existingAssignments)
        {
            var existingRange = new DateRange(existing.EffectiveFrom, existing.EffectiveTo ?? DateOnly.MaxValue);

            if (newRange.Overlaps(existingRange))
            {
                var conflictExistingStr = $"{existing.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}..{(existing.EffectiveTo.HasValue ? existing.EffectiveTo.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "open")}";
                var requestedRangeStr = $"{request.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}..{(request.EffectiveTo.HasValue ? request.EffectiveTo.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "open")}";

                return new Error(
                    ErrorKind.UnprocessableEntity,
                    "area_assignment_overlap",
                    $"The requested assignment period ({requestedRangeStr}) overlaps with an existing assignment ({conflictExistingStr}) for driver '{request.DriverId}' in area '{area.Name}'.",
                    new Dictionary<string, string[]>
                    {
                        ["conflictPeriod"] = [conflictExistingStr],
                        ["requestedPeriod"] = [requestedRangeStr]
                    });
            }
        }

        var assignment = new AreaAssignment(
            Guid.NewGuid(),
            request.AreaId,
            request.DriverId,
            request.EffectiveFrom,
            request.EffectiveTo);

        await areaRepository.AddAssignmentAsync(assignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return assignment.Id;
    }
}
