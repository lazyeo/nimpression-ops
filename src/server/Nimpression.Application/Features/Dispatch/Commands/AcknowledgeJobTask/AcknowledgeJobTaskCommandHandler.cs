using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Application.Features.Dispatch.Commands.AcknowledgeJobTask;

/// <summary>
/// 司机确认派发任务处理器（F5.2 / F5.3）。
/// 状态流转：Draft -> Assigned -> Acknowledged。
/// 非法状态跃迁翻译为 422 UnprocessableEntity。
/// </summary>
public sealed class AcknowledgeJobTaskCommandHandler(
    IJobTaskRepository jobTaskRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<AcknowledgeJobTaskCommand, Result>
{
    public async Task<Result> Handle(AcknowledgeJobTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await jobTaskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
        {
            return Error.NotFound("job_task_not_found", $"Job task '{request.TaskId}' was not found.");
        }

        // 越权校验：司机仅能确认指派给自己的任务，越权返回 403 Forbidden
        if (currentUser.Role == UserRole.Driver && currentUser.UserId.HasValue)
        {
            var driverId = await jobTaskRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!driverId.HasValue || task.DriverId != driverId.Value)
            {
                return Error.Forbidden("forbidden", "You cannot acknowledge a task assigned to another driver.");
            }
        }

        try
        {
            task.Acknowledge(request.AcknowledgedAt ?? dateTimeProvider.UtcNow);
        }
        catch (InvalidJobTaskTransitionException ex)
        {
            return Error.Unprocessable("invalid_task_transition", ex.Message);
        }

        jobTaskRepository.UpdateJobTask(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
