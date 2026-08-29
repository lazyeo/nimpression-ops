using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Fines.Abstractions;
using Nimpression.Application.Features.Fines.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Fines.Queries.GetFinesList;

public sealed class GetFinesListQueryHandler(
    IFineRepository fineRepository,
    ICurrentUser currentUser) : IRequestHandler<GetFinesListQuery, Result<PagedResult<FineDto>>>
{
    public async Task<Result<PagedResult<FineDto>>> Handle(GetFinesListQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;

        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var ownDriverId = await fineRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue)
            {
                return Error.NotFound("driver_not_found", "Driver profile was not found.");
            }

            // IDOR 越权防护：司机尝试指定他人 DriverId 查列表，直接返回 403
            if (filter.DriverId.HasValue && filter.DriverId.Value != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers are not authorized to view other drivers' fines.");
            }

            filter = filter with { DriverId = ownDriverId.Value };
        }

        var result = await fineRepository.GetFinesListAsync(filter, cancellationToken);
        return result;
    }
}
