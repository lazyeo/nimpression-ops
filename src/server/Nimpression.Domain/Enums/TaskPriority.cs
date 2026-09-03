using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 派发任务优先级。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4,
}
