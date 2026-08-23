using System.Text.RegularExpressions;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.ValueObjects;

/// <summary>
/// 电子邮箱值对象。自动规范化为小写去空格，校验基本邮件结构。
/// </summary>
public readonly partial record struct EmailAddress : IComparable<EmailAddress>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Email address cannot be null or empty.");
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > 254 || !EmailRegex.IsMatch(normalized))
        {
            throw new DomainValidationException($"Invalid email address format: '{value}'.");
        }

        Value = normalized;
    }

    public static bool operator <(EmailAddress left, EmailAddress right) => left.CompareTo(right) < 0;
    public static bool operator <=(EmailAddress left, EmailAddress right) => left.CompareTo(right) <= 0;
    public static bool operator >(EmailAddress left, EmailAddress right) => left.CompareTo(right) > 0;
    public static bool operator >=(EmailAddress left, EmailAddress right) => left.CompareTo(right) >= 0;

    public int CompareTo(EmailAddress other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    public override string ToString() => Value;

    public static implicit operator string(EmailAddress email) => email.Value;
}
