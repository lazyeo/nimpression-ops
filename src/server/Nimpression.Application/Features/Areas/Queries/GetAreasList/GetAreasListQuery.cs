using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Application.Features.Areas.DTOs;

namespace Nimpression.Application.Features.Areas.Queries.GetAreasList;

public sealed record GetAreasListQuery(AreaFilter Filter) : IRequest<Result<PagedResult<AreaDto>>>;

public sealed class GetAreasListQueryHandler(
    IAreaRepository areaRepository) : IRequestHandler<GetAreasListQuery, Result<PagedResult<AreaDto>>>
{
    public async Task<Result<PagedResult<AreaDto>>> Handle(GetAreasListQuery request, CancellationToken cancellationToken)
    {
        var result = await areaRepository.GetAreasPagedAsync(request.Filter, cancellationToken);
        return result;
    }
}
