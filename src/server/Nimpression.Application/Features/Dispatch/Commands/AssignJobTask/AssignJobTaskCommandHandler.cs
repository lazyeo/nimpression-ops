using System.Globalization;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Application.Features.Dispatch.Commands.AssignJobTask;

/// <summary>
/// 指派任务命令处理器（F5.1 / F4.3）。
/// </summary>
public sealed class AssignJobTaskCommandHandler(
    IJobTaskRepository jobTaskRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IAuditSink auditSink) : IRequestHandler<AssignJobTaskCommand, Result>
{
    public async Task<Result> Handle(AssignJobTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await jobTaskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
        {
            return Error.NotFound("job_task_not_found", $"Job task '{request.TaskId}' was not found.");
        }

        var driverExists = await jobTaskRepository.DriverExistsAsync(request.DriverId, cancellationToken);
        if (!driverExists)
        {
            return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId}' was not found.");
        }

        var vehicleExists = await jobTaskRepository.VehicleExistsAsync(request.VehicleId, cancellationToken);
        if (!vehicleExists)
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle with ID '{request.VehicleId}' was not found.");
        }

        // F4.3: 派单约束判定
        var scheduledTime = request.ScheduledFor ?? task.ScheduledFor;
        var scheduledDate = DateOnly.FromDateTime(scheduledTime.DateTime);
        var isAssignedToArea = await jobTaskRepository.IsDriverAssignedToAreaOnDateAsync(
            request.DriverId,
            task.AreaId,
            scheduledDate,
            cancellationToken);

        if (!isAssignedToArea)
        {
            if (!request.OverrideAreaWarning)
            {
                return Error.Unprocessable(
                    "area_mismatch_warning",
                    $"Driver '{request.DriverId}' is not currently assigned to area '{task.AreaId}' on {scheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}. Confirm override to proceed.");
            }

            // F4.3: 越过行为写审计
            await auditSink.RecordAsync(
                "JobTask",
                task.Id,
                "OverrideAreaWarning",
                beforeJson: null,
                afterJson: $"{{\"DriverId\":\"{request.DriverId}\",\"AreaId\":\"{task.AreaId}\",\"ScheduledFor\":\"{scheduledTime:O}\"}}",
                cancellationToken);
        }

        try
        {
            task.Assign(request.DriverId, request.VehicleId, request.ScheduledFor, dateTimeProvider.UtcNow);
        }
        catch (InvalidJobTaskTransitionException ex)
        {
            return Error.Unprocessable("invalid_task_transition", ex.Message);
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_assignment", ex.Message);
        }

        jobTaskRepository.UpdateJobTask(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
