namespace Nimpression.Domain.Enums;

/// <summary>
/// 派发任务生命周期状态。
/// </summary>
public enum JobTaskStatus
{
    Draft = 1,
    Assigned = 2,
    Acknowledged = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6,
}
