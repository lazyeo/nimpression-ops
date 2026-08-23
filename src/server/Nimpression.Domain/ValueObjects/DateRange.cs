using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.ValueObjects;

/// <summary>
/// 日期区间值对象（闭区间 [Start, End]）。强制 End >= Start，提供重叠判定。
/// </summary>
public readonly record struct DateRange
{
    public DateOnly Start { get; }
    public DateOnly End { get; }

    public DateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new DomainValidationException(
                $"DateRange end date ({end:yyyy-MM-dd}) cannot be earlier than start date ({start:yyyy-MM-dd}).");
        }

        Start = start;
        End = end;
    }

    /// <summary>
    /// 区间包含的天数（闭区间）。
    /// </summary>
    public int LengthInDays => End.DayNumber - Start.DayNumber + 1;

    /// <summary>
    /// 检查指定日期是否落在本区间内。
    /// </summary>
    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>
    /// 检查与另一区间是否存在交集。
    /// </summary>
    public bool Overlaps(DateRange other) => Start <= other.End && other.Start <= End;

    public override string ToString() => $"{Start:yyyy-MM-dd}..{End:yyyy-MM-dd}";
}
