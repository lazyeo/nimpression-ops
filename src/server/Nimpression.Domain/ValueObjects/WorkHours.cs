using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.ValueObjects;

/// <summary>
/// 工时值对象。非负，精确到最多 2 位小数。
/// </summary>
public readonly record struct WorkHours : IComparable<WorkHours>
{
    public decimal Value { get; }

    public WorkHours(decimal value)
    {
        if (value < 0m)
        {
            throw new DomainValidationException($"WorkHours cannot be negative: {value}.");
        }

        Value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static WorkHours Zero => new(0m);

    public static WorkHours FromMinutes(int minutes)
    {
        if (minutes < 0)
        {
            throw new DomainValidationException($"Minutes cannot be negative: {minutes}.");
        }

        return new WorkHours(minutes / 60m);
    }

    public static WorkHours operator +(WorkHours left, WorkHours right)
    {
        return new WorkHours(left.Value + right.Value);
    }

    public static WorkHours operator -(WorkHours left, WorkHours right)
    {
        if (left.Value < right.Value)
        {
            throw new DomainValidationException(
                $"Cannot subtract larger WorkHours ({right.Value}) from smaller ({left.Value}).");
        }

        return new WorkHours(left.Value - right.Value);
    }

    public static WorkHours operator *(WorkHours hours, decimal multiplier)
    {
        if (multiplier < 0m)
        {
            throw new DomainValidationException($"Multiplier cannot be negative: {multiplier}.");
        }

        return new WorkHours(hours.Value * multiplier);
    }

    public static WorkHours operator *(decimal multiplier, WorkHours hours)
    {
        return hours * multiplier;
    }

    public static WorkHours operator /(WorkHours hours, decimal divisor)
    {
        if (divisor <= 0m)
        {
            throw new DomainValidationException($"Divisor must be greater than zero: {divisor}.");
        }

        return new WorkHours(hours.Value / divisor);
    }

    public static bool operator >(WorkHours left, WorkHours right) => left.Value > right.Value;
    public static bool operator <(WorkHours left, WorkHours right) => left.Value < right.Value;
    public static bool operator >=(WorkHours left, WorkHours right) => left.Value >= right.Value;
    public static bool operator <=(WorkHours left, WorkHours right) => left.Value <= right.Value;

    public int CompareTo(WorkHours other) => Value.CompareTo(other.Value);

    public override string ToString() => $"{Value:0.##} h";
}
