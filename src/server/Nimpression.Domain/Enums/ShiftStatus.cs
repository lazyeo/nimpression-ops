using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 班次打卡状态。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShiftStatus
{
    Active = 1,
    Completed = 2,
    AutoClosed = 3,
    Cancelled = 4,
}
