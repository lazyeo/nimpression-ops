using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 系统用户角色。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    Admin = 1,
    Dispatcher = 2,
    Driver = 3,
}
