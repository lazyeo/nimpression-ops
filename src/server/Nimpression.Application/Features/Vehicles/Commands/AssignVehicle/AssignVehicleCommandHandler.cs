using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Application.Features.Vehicles.Common;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Vehicles.Commands.AssignVehicle;

/// <summary>
/// 车辆指派命令处理器。
/// 
/// 关键并发保证：
/// 绝对不使用"先查询是否存在未释放分派再写入"的模式，因为先查后写存在 TOCTOU（Time-of-Check to Time-of-Use）
/// 竞态条件，在并发请求下会导致多个未释放分派同时插入。
/// 本处理器直接插入分派记录，由数据库层的部分唯一索引（WHERE "ReleasedAt" IS NULL）硬担保排他性，
/// 并捕获 PostgreSQL 唯一约束违规（SqlState 23505）翻译为 Error.Conflict (409)。
/// </summary>
public sealed class AssignVehicleCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<AssignVehicleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AssignVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle '{request.VehicleId}' was not found.");
        }

        if (vehicle.Status == VehicleStatus.Decommissioned)
        {
            return Error.Unprocessable("vehicle_decommissioned", "Cannot assign a decommissioned vehicle.");
        }

        var driverExists = await vehicleRepository.DriverExistsAsync(request.DriverId, cancellationToken);
        if (!driverExists)
        {
            return Error.NotFound("driver_not_found", $"Driver '{request.DriverId}' was not found.");
        }

        var assignedByUserId = currentUser.UserId ?? Guid.Empty;
        if (assignedByUserId == Guid.Empty)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        var assignedAt = request.AssignedAt ?? dateTimeProvider.UtcNow;
        var assignment = new VehicleAssignment(
            Guid.NewGuid(),
            request.VehicleId,
            request.DriverId,
            assignedAt,
            assignedByUserId);

        try
        {
            await vehicleRepository.AddAssignmentAsync(assignment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            // 捕获 PostgreSQL 唯一约束违反 (SqlState 23505) 并返回 409 Conflict
            return Error.Conflict("vehicle_already_assigned", "Vehicle is currently assigned to an active driver and has not been released.");
        }

        return assignment.Id;
    }
}
