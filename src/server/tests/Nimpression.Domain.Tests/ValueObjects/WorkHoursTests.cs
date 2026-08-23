using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.ValueObjects;

public sealed class WorkHoursTests
{
    [Fact]
    public void WorkHours_initializes_and_rounds_to_two_decimals()
    {
        var h = new WorkHours(8.125m);
        Assert.Equal(8.13m, h.Value);
        Assert.Equal("8.13 h", h.ToString());
    }

    [Fact]
    public void WorkHours_from_minutes()
    {
        var h = WorkHours.FromMinutes(90);
        Assert.Equal(1.50m, h.Value);

        var h2 = WorkHours.FromMinutes(480);
        Assert.Equal(8.00m, h2.Value);

        Assert.Throws<DomainValidationException>(() => WorkHours.FromMinutes(-10));
    }

    [Fact]
    public void WorkHours_throws_on_negative()
    {
        Assert.Throws<DomainValidationException>(() => new WorkHours(-0.5m));
    }

    [Fact]
    public void WorkHours_arithmetic_operations()
    {
        var h1 = new WorkHours(8m);
        var h2 = new WorkHours(2.5m);

        var sum = h1 + h2;
        Assert.Equal(10.5m, sum.Value);

        var diff = h1 - h2;
        Assert.Equal(5.5m, diff.Value);

        Assert.Throws<DomainValidationException>(() => _ = h2 - h1);

        var mult = h2 * 2m;
        Assert.Equal(5m, mult.Value);

        var multLeft = 1.5m * h1;
        Assert.Equal(12m, multLeft.Value);

        var div = h1 / 2m;
        Assert.Equal(4m, div.Value);

        Assert.Throws<DomainValidationException>(() => _ = h1 / 0m);
        Assert.Throws<DomainValidationException>(() => _ = h1 * -1m);
    }

    [Fact]
    public void WorkHours_comparisons_and_zero()
    {
        var h1 = new WorkHours(8m);
        var h2 = new WorkHours(10m);
        var h3 = new WorkHours(8m);

        Assert.True(h1 < h2);
        Assert.True(h2 > h1);
        Assert.True(h1 <= h3);
        Assert.True(h1 >= h3);
        Assert.Equal(0, h1.CompareTo(h3));
        Assert.True(h1.CompareTo(h2) < 0);

        Assert.Equal(0m, WorkHours.Zero.Value);
    }
}
