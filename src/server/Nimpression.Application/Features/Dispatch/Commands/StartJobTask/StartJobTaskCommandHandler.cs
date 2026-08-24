using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Dispatch.Commands.StartJobTask;

/// <summary>
/// 开始执行任务命令处理器（F5.2 / F5.3）。
/// </summary>
public sealed class StartJobTaskCommandHandler(
    IJobTaskRepository jobTaskRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<StartJobTaskCommand, Result>
{
    public async Task<Result> Handle(StartJobTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await jobTaskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
        {
            return Error.NotFound("job_task_not_found", $"Job task '{request.TaskId}' was not found.");
        }

        if (currentUser.Role == UserRole.Driver && currentUser.UserId.HasValue)
        {
            var driverId = await jobTaskRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!driverId.HasValue || task.DriverId != driverId.Value)
            {
                return Error.Forbidden("forbidden", "You cannot start a task assigned to another driver.");
            }
        }

        try
        {
            task.Start(
                request.StartedAt ?? dateTimeProvider.UtcNow,
                request.StartOdometerKm.HasValue ? new Kilometres(request.StartOdometerKm.Value) : null);
        }
        catch (InvalidJobTaskTransitionException ex)
        {
            return Error.Unprocessable("invalid_task_transition", ex.Message);
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_start_data", ex.Message);
        }

        jobTaskRepository.UpdateJobTask(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
