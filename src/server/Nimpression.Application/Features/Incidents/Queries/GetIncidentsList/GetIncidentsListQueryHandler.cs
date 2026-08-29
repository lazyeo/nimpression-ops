using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Incidents.Abstractions;
using Nimpression.Application.Features.Incidents.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Incidents.Queries.GetIncidentsList;

public sealed class GetIncidentsListQueryHandler(
    IIncidentRepository incidentRepository,
    ICurrentUser currentUser) : IRequestHandler<GetIncidentsListQuery, Result<PagedResult<IncidentReportDto>>>
{
    public async Task<Result<PagedResult<IncidentReportDto>>> Handle(GetIncidentsListQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;

        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var ownDriverId = await incidentRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue)
            {
                return Error.NotFound("driver_not_found", "Driver profile was not found.");
            }

            if (filter.DriverId.HasValue && filter.DriverId.Value != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers are not authorized to view other drivers' incident reports.");
            }

            filter = filter with { DriverId = ownDriverId.Value };
        }

        var result = await incidentRepository.GetIncidentsListAsync(filter, cancellationToken);
        return result;
    }
}
