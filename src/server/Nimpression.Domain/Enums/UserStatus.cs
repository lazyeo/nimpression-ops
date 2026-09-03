using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 系统用户状态。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
}
