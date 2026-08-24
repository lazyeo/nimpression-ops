using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Application.Features.Dispatch.Commands.CancelJobTask;

/// <summary>
/// 取消派发任务命令处理器（F5.3）。
/// </summary>
public sealed class CancelJobTaskCommandHandler(
    IJobTaskRepository jobTaskRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<CancelJobTaskCommand, Result>
{
    public async Task<Result> Handle(CancelJobTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await jobTaskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
        {
            return Error.NotFound("job_task_not_found", $"Job task '{request.TaskId}' was not found.");
        }

        try
        {
            task.Cancel(request.Reason, request.CancelledAt ?? dateTimeProvider.UtcNow);
        }
        catch (InvalidJobTaskTransitionException ex)
        {
            return Error.Unprocessable("invalid_task_transition", ex.Message);
        }
        catch (DomainValidationException ex)
        {
            return Error.Unprocessable("invalid_cancellation_reason", ex.Message);
        }

        jobTaskRepository.UpdateJobTask(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
