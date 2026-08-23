using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.ValueObjects;

/// <summary>
/// 公里数/里程值对象。必须为非负数。
/// </summary>
public readonly record struct Kilometres : IComparable<Kilometres>
{
    public decimal Value { get; }

    public Kilometres(decimal value)
    {
        if (value < 0m)
        {
            throw new DomainValidationException($"Kilometres cannot be negative: {value}.");
        }

        Value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static Kilometres Zero => new(0m);

    public static Kilometres operator +(Kilometres left, Kilometres right)
    {
        return new Kilometres(left.Value + right.Value);
    }

    public static Kilometres operator -(Kilometres left, Kilometres right)
    {
        if (left.Value < right.Value)
        {
            throw new DomainValidationException(
                $"Cannot subtract larger Kilometres ({right.Value}) from smaller ({left.Value}).");
        }

        return new Kilometres(left.Value - right.Value);
    }

    public static Kilometres operator *(Kilometres km, decimal multiplier)
    {
        if (multiplier < 0m)
        {
            throw new DomainValidationException($"Multiplier cannot be negative: {multiplier}.");
        }

        return new Kilometres(km.Value * multiplier);
    }

    public static Kilometres operator *(decimal multiplier, Kilometres km)
    {
        return km * multiplier;
    }

    public static bool operator >(Kilometres left, Kilometres right) => left.Value > right.Value;
    public static bool operator <(Kilometres left, Kilometres right) => left.Value < right.Value;
    public static bool operator >=(Kilometres left, Kilometres right) => left.Value >= right.Value;
    public static bool operator <=(Kilometres left, Kilometres right) => left.Value <= right.Value;

    public int CompareTo(Kilometres other) => Value.CompareTo(other.Value);

    public override string ToString() => $"{Value:0.##} km";
}
