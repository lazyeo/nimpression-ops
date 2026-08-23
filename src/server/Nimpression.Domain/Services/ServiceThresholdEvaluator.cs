using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Services;

/// <summary>
/// 车辆保养阈值评估结果。
/// </summary>
public sealed record ServiceEvaluationResult(
    bool NeedsService,
    int ServiceCycleNo,
    string IdempotencyKey,
    Kilometres OverdueByKm,
    ServiceThresholdReached? DomainEvent);

/// <summary>
/// 车辆保养阈值评估领域服务（纯逻辑，无 IO）。
/// 计算里程是否超出保养间隔并生成防重复发送保养邮件的幂等键。
/// </summary>
public static class ServiceThresholdEvaluator
{
    /// <summary>
    /// 评估车辆是否达到保养阈值。
    /// </summary>
    public static ServiceEvaluationResult Evaluate(
        Guid vehicleId,
        Kilometres currentOdometer,
        Kilometres lastServiceOdometer,
        Kilometres serviceInterval,
        DateTimeOffset? evaluationTime = null)
    {
        if (vehicleId == Guid.Empty)
        {
            throw new DomainValidationException("VehicleId cannot be empty.");
        }

        if (serviceInterval.Value <= 0m)
        {
            throw new DomainValidationException("Service interval must be greater than zero.");
        }

        if (currentOdometer < lastServiceOdometer)
        {
            throw new DomainValidationException(
                $"Current odometer ({currentOdometer.Value} km) cannot be less than last service odometer ({lastServiceOdometer.Value} km).");
        }

        var distanceSinceLastService = currentOdometer - lastServiceOdometer;
        var needsService = distanceSinceLastService >= serviceInterval;
        var cycleNo = (int)(currentOdometer.Value / serviceInterval.Value);
        var idempotencyKey = $"SERVICE_{vehicleId:D}_{cycleNo}";
        var overdueBy = needsService ? distanceSinceLastService - serviceInterval : Kilometres.Zero;
        var thresholdKm = lastServiceOdometer + serviceInterval;
        var now = evaluationTime ?? DateTimeOffset.UtcNow;

        var domainEvent = needsService
            ? new ServiceThresholdReached(vehicleId, cycleNo, currentOdometer, thresholdKm, now)
            : null;

        return new ServiceEvaluationResult(
            needsService,
            cycleNo,
            idempotencyKey,
            overdueBy,
            domainEvent);
    }

    /// <summary>
    /// 根据 Vehicle 实体评估保养状态。
    /// </summary>
    public static ServiceEvaluationResult Evaluate(Vehicle vehicle, DateTimeOffset? evaluationTime = null)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        return Evaluate(
            vehicle.Id,
            vehicle.OdometerKm,
            vehicle.LastServiceOdometerKm,
            vehicle.ServiceIntervalKm,
            evaluationTime);
    }
}
