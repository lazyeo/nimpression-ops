using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;

namespace Nimpression.Application.Features.Dispatch.Queries.GetJobTasksList;

public sealed record GetJobTasksListQuery(JobTaskFilter Filter) : IRequest<Result<PagedResult<JobTaskSummaryDto>>>;

public sealed class GetJobTasksListQueryHandler(
    IJobTaskRepository jobTaskRepository) : IRequestHandler<GetJobTasksListQuery, Result<PagedResult<JobTaskSummaryDto>>>
{
    public async Task<Result<PagedResult<JobTaskSummaryDto>>> Handle(GetJobTasksListQuery request, CancellationToken cancellationToken)
    {
        var result = await jobTaskRepository.GetJobTasksPagedAsync(request.Filter, cancellationToken);
        return result;
    }
}
