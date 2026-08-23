using System.Globalization;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.ValueObjects;

public sealed class DateRangeTests
{
    [Fact]
    public void DateRange_initializes_and_calculates_length()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 10);
        var range = new DateRange(start, end);

        Assert.Equal(start, range.Start);
        Assert.Equal(end, range.End);
        Assert.Equal(10, range.LengthInDays);
        Assert.Equal("2026-01-01..2026-01-10", range.ToString());
    }

    [Fact]
    public void DateRange_single_day()
    {
        var day = new DateOnly(2026, 3, 15);
        var range = new DateRange(day, day);
        Assert.Equal(1, range.LengthInDays);
        Assert.True(range.Contains(day));
    }

    [Fact]
    public void DateRange_throws_when_end_is_before_start()
    {
        var start = new DateOnly(2026, 5, 10);
        var end = new DateOnly(2026, 5, 9);
        Assert.Throws<DomainValidationException>(() => new DateRange(start, end));
    }

    [Fact]
    public void DateRange_contains_boundary_and_outside_dates()
    {
        var range = new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        Assert.True(range.Contains(new DateOnly(2026, 6, 1)));
        Assert.True(range.Contains(new DateOnly(2026, 6, 15)));
        Assert.True(range.Contains(new DateOnly(2026, 6, 30)));
        Assert.False(range.Contains(new DateOnly(2026, 5, 31)));
        Assert.False(range.Contains(new DateOnly(2026, 7, 1)));
    }

    [Theory]
    [InlineData("2026-01-01", "2026-01-10", "2026-01-05", "2026-01-15", true)]   // partial overlap
    [InlineData("2026-01-01", "2026-01-10", "2026-01-10", "2026-01-20", true)]   // boundary touch
    [InlineData("2026-01-05", "2026-01-08", "2026-01-01", "2026-01-10", true)]   // subset
    [InlineData("2026-01-01", "2026-01-10", "2026-01-11", "2026-01-20", false)]  // disjoint after
    [InlineData("2026-01-15", "2026-01-20", "2026-01-01", "2026-01-10", false)]  // disjoint before
    public void DateRange_overlaps_matrix(
        string s1, string e1, string s2, string e2, bool expectedOverlap)
    {
        var r1 = new DateRange(DateOnly.Parse(s1, CultureInfo.InvariantCulture), DateOnly.Parse(e1, CultureInfo.InvariantCulture));
        var r2 = new DateRange(DateOnly.Parse(s2, CultureInfo.InvariantCulture), DateOnly.Parse(e2, CultureInfo.InvariantCulture));

        Assert.Equal(expectedOverlap, r1.Overlaps(r2));
        Assert.Equal(expectedOverlap, r2.Overlaps(r1));
    }
}
