using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.ValueObjects;

public sealed class KilometresTests
{
    [Fact]
    public void Kilometres_initializes_and_formats_correctly()
    {
        var km = new Kilometres(125.456m);
        Assert.Equal(125.46m, km.Value);
        Assert.Equal("125.46 km", km.ToString());
    }

    [Fact]
    public void Kilometres_throws_on_negative_value()
    {
        Assert.Throws<DomainValidationException>(() => new Kilometres(-0.01m));
        Assert.Throws<DomainValidationException>(() => new Kilometres(-100m));
    }

    [Fact]
    public void Kilometres_arithmetic_operations()
    {
        var k1 = new Kilometres(50m);
        var k2 = new Kilometres(30m);

        var sum = k1 + k2;
        Assert.Equal(80m, sum.Value);

        var diff = k1 - k2;
        Assert.Equal(20m, diff.Value);

        Assert.Throws<DomainValidationException>(() => _ = k2 - k1);

        var mult = k1 * 2.5m;
        Assert.Equal(125m, mult.Value);

        var multLeft = 3m * k2;
        Assert.Equal(90m, multLeft.Value);

        Assert.Throws<DomainValidationException>(() => _ = k1 * -1m);
    }

    [Fact]
    public void Kilometres_comparisons_work()
    {
        var k1 = new Kilometres(100m);
        var k2 = new Kilometres(200m);
        var k3 = new Kilometres(100m);

        Assert.True(k1 < k2);
        Assert.True(k2 > k1);
        Assert.True(k1 <= k3);
        Assert.True(k1 >= k3);
        Assert.Equal(0, k1.CompareTo(k3));
        Assert.True(k1.CompareTo(k2) < 0);
    }

    [Fact]
    public void Kilometres_Zero_returns_zero_value()
    {
        var zero = Kilometres.Zero;
        Assert.Equal(0m, zero.Value);
    }
}
