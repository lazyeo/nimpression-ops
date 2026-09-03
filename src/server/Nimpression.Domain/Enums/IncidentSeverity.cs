using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 事故严重程度。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncidentSeverity
{
    Minor = 1,
    Moderate = 2,
    Major = 3,
    Critical = 4,
}
