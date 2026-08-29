using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Application.Features.Areas.DTOs;

namespace Nimpression.Application.Features.Areas.Queries.GetAreaById;

public sealed record GetAreaByIdQuery(Guid Id) : IRequest<Result<AreaDetailDto>>;

public sealed class GetAreaByIdQueryHandler(
    IAreaRepository areaRepository,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetAreaByIdQuery, Result<AreaDetailDto>>
{
    public async Task<Result<AreaDetailDto>> Handle(GetAreaByIdQuery request, CancellationToken cancellationToken)
    {
        var area = await areaRepository.GetAreaDetailByIdAsync(
            request.Id,
            dateTimeProvider.NzToday,
            cancellationToken);

        if (area is null)
        {
            return Error.NotFound("area_not_found", $"Area with ID '{request.Id}' was not found.");
        }

        return area;
    }
}
