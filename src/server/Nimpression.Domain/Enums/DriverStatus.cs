using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 司机雇佣状态。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DriverStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    OnLeave = 4,
    Terminated = 5,
}
