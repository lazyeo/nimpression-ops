using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Application.Features.Areas.DTOs;

namespace Nimpression.Application.Features.Areas.Queries.GetAreaAssignments;

public sealed record GetAreaAssignmentsQuery(
    Guid? AreaId = null,
    Guid? DriverId = null) : IRequest<Result<List<AreaAssignmentDto>>>;

public sealed class GetAreaAssignmentsQueryHandler(
    IAreaRepository areaRepository,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetAreaAssignmentsQuery, Result<List<AreaAssignmentDto>>>
{
    public async Task<Result<List<AreaAssignmentDto>>> Handle(GetAreaAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var assignments = await areaRepository.GetAreaAssignmentsAsync(
            request.AreaId,
            request.DriverId,
            dateTimeProvider.NzToday,
            cancellationToken);

        return assignments;
    }
}
