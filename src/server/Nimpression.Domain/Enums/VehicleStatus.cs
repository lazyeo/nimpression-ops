using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 车辆运营与维护状态。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VehicleStatus
{
    Active = 1,
    Maintenance = 2,
    Inactive = 3,
    Decommissioned = 4,
}
