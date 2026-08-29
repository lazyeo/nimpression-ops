using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;

namespace Nimpression.Application.Features.Dispatch.Queries.GetJobTaskById;

public sealed record GetJobTaskByIdQuery(Guid Id) : IRequest<Result<JobTaskDetailDto>>;

public sealed class GetJobTaskByIdQueryHandler(
    IJobTaskRepository jobTaskRepository) : IRequestHandler<GetJobTaskByIdQuery, Result<JobTaskDetailDto>>
{
    public async Task<Result<JobTaskDetailDto>> Handle(GetJobTaskByIdQuery request, CancellationToken cancellationToken)
    {
        var detail = await jobTaskRepository.GetJobTaskDetailByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            return Error.NotFound("job_task_not_found", $"Job task with ID '{request.Id}' was not found.");
        }

        return detail;
    }
}
