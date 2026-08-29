namespace Nimpression.Application.Features.Realtime.Common;

/// <summary>
/// 实时失效信号类型定义常量。
/// 仅标识“什么类型的实体发生了变更”，绝不携带实体业务数据。
/// </summary>
public static class RealtimeEventKinds
{
    public const string TaskAssigned = "task.assigned";
    public const string TaskAcknowledged = "task.acknowledged";
    public const string TaskCompleted = "task.completed";
    public const string DriverDeactivated = "driver.deactivated";
    public const string IncidentReported = "incident.reported";
    public const string FineAccepted = "fine.accepted";
    public const string PayslipFinalised = "payslip.finalised";
    public const string NewsPublished = "news.published";
    public const string VehicleServiceThresholdReached = "vehicle.service_threshold_reached";
    public const string GenericDomainEvent = "domain.event";
}
