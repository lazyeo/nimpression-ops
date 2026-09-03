using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 外部合作方类别。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PartnerKind
{
    Insurer = 1,
    Maintenance = 2,
    Inspection = 3,
}
