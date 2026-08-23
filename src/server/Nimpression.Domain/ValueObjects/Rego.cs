using System.Text.RegularExpressions;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.ValueObjects;

/// <summary>
/// 新西兰车辆车牌号（Rego）值对象。自动规范化为大写去空格，校验有效格式。
/// </summary>
public readonly partial record struct Rego : IComparable<Rego>
{
    private static readonly Regex PlateRegex = new("^[A-Z0-9]{1,6}$", RegexOptions.Compiled);

    public string Value { get; }

    public Rego(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Vehicle registration (Rego) cannot be null or empty.");
        }

        var normalized = value.Trim().Replace(" ", string.Empty).ToUpperInvariant();

        if (!PlateRegex.IsMatch(normalized))
        {
            throw new DomainValidationException(
                $"Invalid NZ registration plate format: '{value}'. Must be 1-6 alphanumeric characters.");
        }

        Value = normalized;
    }

    public static bool operator <(Rego left, Rego right) => left.CompareTo(right) < 0;
    public static bool operator <=(Rego left, Rego right) => left.CompareTo(right) <= 0;
    public static bool operator >(Rego left, Rego right) => left.CompareTo(right) > 0;
    public static bool operator >=(Rego left, Rego right) => left.CompareTo(right) >= 0;

    public int CompareTo(Rego other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    public override string ToString() => Value;

    public static implicit operator string(Rego rego) => rego.Value;
}
