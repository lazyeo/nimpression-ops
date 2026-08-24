using System.Globalization;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.Common;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Dispatch.Commands.CreateJobTask;

/// <summary>
/// 创建派发任务命令处理器（F5.1 / F4.3）。
/// </summary>
public sealed class CreateJobTaskCommandHandler(
    IJobTaskRepository jobTaskRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditSink auditSink) : IRequestHandler<CreateJobTaskCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateJobTaskCommand request, CancellationToken cancellationToken)
    {
        var areaExists = await jobTaskRepository.AreaExistsAsync(request.AreaId, cancellationToken);
        if (!areaExists)
        {
            return Error.NotFound("area_not_found", $"Area with ID '{request.AreaId}' was not found.");
        }

        var createdByUserId = currentUser.UserId ?? Guid.Empty;
        if (createdByUserId == Guid.Empty)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        if (request.DriverId.HasValue && request.VehicleId.HasValue)
        {
            var driverExists = await jobTaskRepository.DriverExistsAsync(request.DriverId.Value, cancellationToken);
            if (!driverExists)
            {
                return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId.Value}' was not found.");
            }

            var vehicleExists = await jobTaskRepository.VehicleExistsAsync(request.VehicleId.Value, cancellationToken);
            if (!vehicleExists)
            {
                return Error.NotFound("vehicle_not_found", $"Vehicle with ID '{request.VehicleId.Value}' was not found.");
            }

            // F4.3: 派单约束判定
            var scheduledDate = DateOnly.FromDateTime(request.ScheduledFor.DateTime);
            var isAssignedToArea = await jobTaskRepository.IsDriverAssignedToAreaOnDateAsync(
                request.DriverId.Value,
                request.AreaId,
                scheduledDate,
                cancellationToken);

            if (!isAssignedToArea)
            {
                if (!request.OverrideAreaWarning)
                {
                    return Error.Unprocessable(
                        "area_mismatch_warning",
                        $"Driver '{request.DriverId.Value}' is not currently assigned to area '{request.AreaId}' on {scheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}. Confirm override to proceed.");
                }

                // F4.3: 越过行为写审计
                await auditSink.RecordAsync(
                    "JobTask",
                    null,
                    "OverrideAreaWarning",
                    beforeJson: null,
                    afterJson: $"{{\"DriverId\":\"{request.DriverId.Value}\",\"AreaId\":\"{request.AreaId}\",\"ScheduledFor\":\"{request.ScheduledFor:O}\"}}",
                    cancellationToken);
            }
        }

        var refCode = string.IsNullOrWhiteSpace(request.Ref)
            ? $"TSK-{request.ScheduledFor:yyyyMMdd}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..6].ToUpperInvariant()}"
            : request.Ref.Trim().ToUpperInvariant();

        JobTask task;
        try
        {
            task = new JobTask(
                Guid.NewGuid(),
                refCode,
                request.Title,
                request.AreaId,
                request.ScheduledFor,
                createdByUserId,
                request.Description,
                request.Priority,
                request.PlannedDistanceKm.HasValue ? new Kilometres(request.PlannedDistanceKm.Value) : null,
                request.DriverId,
                request.VehicleId);
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_task_data", ex.Message);
        }

        try
        {
            await jobTaskRepository.AddJobTaskAsync(task, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            return Error.Conflict("job_task_ref_conflict", $"Job task with reference '{refCode}' already exists.");
        }

        return task.Id;
    }
}
