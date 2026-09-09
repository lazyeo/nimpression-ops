using Nimpression.Domain.Exceptions;
using Nimpression.Domain.Services;
using Xunit;

namespace Nimpression.Domain.Tests.Services;

public class NzAdultMinimumWageTests
{
    [Theory]
    [InlineData(2024, 4, 1, 23.15)]
    [InlineData(2025, 3, 31, 23.15)]
    [InlineData(2025, 4, 1, 23.50)]
    [InlineData(2026, 3, 31, 23.50)]
    [InlineData(2026, 4, 1, 23.95)]
    [InlineData(2027, 3, 31, 23.95)]
    public void UsesEffectiveWorkDate(int year, int month, int day, decimal expected)
    {
        Assert.Equal(expected, NzAdultMinimumWage.ForWorkDate(new(year, month, day)));
    }

    [Fact]
    public void RejectsUnknownDatesAndMixedRatePeriods()
    {
        Assert.Throws<DomainValidationException>(() => NzAdultMinimumWage.ForWorkDate(new(2024, 3, 31)));
        Assert.Throws<DomainValidationException>(() => NzAdultMinimumWage.ForWorkDate(new(2027, 4, 1)));
        Assert.Throws<DomainValidationException>(() => NzAdultMinimumWage.ForPeriod(new(2026, 3, 30), new(2026, 4, 12)));
    }

    [Fact]
    public void AllowsHigherAgreedMinimum_BlocksBelowStatutory()
    {
        Assert.Equal(25m, NzAdultMinimumWage.ForPeriod(new(2026, 8, 1), new(2026, 8, 14), 25m).Amount);
        Assert.Throws<DomainValidationException>(() => NzAdultMinimumWage.ForPeriod(new(2026, 8, 1), new(2026, 8, 14), 23.15m));
    }
}
