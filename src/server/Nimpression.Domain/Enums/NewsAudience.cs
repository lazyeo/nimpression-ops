using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 新闻公告受众范围。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NewsAudience
{
    All = 1,
    Drivers = 2,
    Dispatchers = 3,
}
