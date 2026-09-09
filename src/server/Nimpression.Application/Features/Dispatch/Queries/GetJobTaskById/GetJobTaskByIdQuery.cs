using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Domain.Enums;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;

namespace Nimpression.Application.Features.Dispatch.Queries.GetJobTaskById;

public sealed record GetJobTaskByIdQuery(Guid Id) : IRequest<Result<JobTaskDetailDto>>;

public sealed class GetJobTaskByIdQueryHandler(
    IJobTaskRepository jobTaskRepository,
    ICurrentUser currentUser) : IRequestHandler<GetJobTaskByIdQuery, Result<JobTaskDetailDto>>
{
    public async Task<Result<JobTaskDetailDto>> Handle(GetJobTaskByIdQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue ||
            currentUser.Role is not (UserRole.Admin or UserRole.Dispatcher or UserRole.Driver))
        {
            return Error.Forbidden("forbidden", "You cannot view this task.");
        }

        var detail = await jobTaskRepository.GetJobTaskDetailByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            return Error.NotFound("job_task_not_found", $"Job task with ID '{request.Id}' was not found.");
        }

        if (currentUser.Role == UserRole.Driver)
        {
            var driverId = await jobTaskRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!driverId.HasValue || detail.DriverId != driverId.Value)
            {
                return Error.Forbidden("forbidden", "You cannot view another driver's task.");
            }
        }

        return detail;
    }
}
