using System.Text.Json.Serialization;

namespace Nimpression.Domain.Enums;

/// <summary>
/// 薪资周期状态。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PayPeriodStatus
{
    Open = 1,
    Calculating = 2,
    Finalised = 3,
    Paid = 4,
}
