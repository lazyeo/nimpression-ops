namespace Nimpression.Application.Features.Dispatch.DTOs;

/// <summary>
/// 司机端任务卡片列表项 DTO（精确对齐前端 DriverTaskItem 契约）。
/// </summary>
public sealed record DriverTaskItemDto(
    Guid Id,
    string TripNo,
    string Status,
    string PickupLocation,
    string DeliveryLocation,
    DateTimeOffset ScheduledTime,
    string VehiclePlate);
