using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Money_initializes_with_default_currency_and_rounds_amount()
    {
        var money = new Money(123.456m);
        Assert.Equal(123.46m, money.Amount);
        Assert.Equal("NZD", money.Currency);
        Assert.Equal("123.46 NZD", money.ToString());
    }

    [Fact]
    public void Money_throws_on_null_or_empty_currency()
    {
        Assert.Throws<DomainValidationException>(() => new Money(100m, ""));
        Assert.Throws<DomainValidationException>(() => new Money(100m, "   "));
    }

    [Fact]
    public void Money_addition_and_subtraction_work_for_same_currency()
    {
        var a = new Money(50.25m, "NZD");
        var b = new Money(25.50m, "NZD");

        var sum = a + b;
        Assert.Equal(75.75m, sum.Amount);
        Assert.Equal("NZD", sum.Currency);

        var diff = a - b;
        Assert.Equal(24.75m, diff.Amount);
        Assert.Equal("NZD", diff.Currency);
    }

    [Fact]
    public void Money_throws_on_cross_currency_addition_or_subtraction()
    {
        var nzd = new Money(100m, "NZD");
        var usd = new Money(100m, "USD");

        Assert.Throws<DomainValidationException>(() => _ = nzd + usd);
        Assert.Throws<DomainValidationException>(() => _ = nzd - usd);
        Assert.Throws<DomainValidationException>(() => _ = nzd > usd);
        Assert.Throws<DomainValidationException>(() => _ = nzd.CompareTo(usd));
    }

    [Fact]
    public void Money_multiplication_and_division()
    {
        var m = new Money(100m, "NZD");
        var multiplied = m * 1.5m;
        Assert.Equal(150m, multiplied.Amount);

        var multipliedLeft = 2m * m;
        Assert.Equal(200m, multipliedLeft.Amount);

        var divided = m / 4m;
        Assert.Equal(25m, divided.Amount);

        Assert.Throws<DivideByZeroException>(() => _ = m / 0m);
    }

    [Fact]
    public void Money_comparisons_work()
    {
        var m1 = new Money(100m, "NZD");
        var m2 = new Money(200m, "NZD");
        var m3 = new Money(100m, "NZD");

        Assert.True(m1 < m2);
        Assert.True(m2 > m1);
        Assert.True(m1 <= m3);
        Assert.True(m1 >= m3);
        Assert.Equal(0, m1.CompareTo(m3));
        Assert.True(m1.CompareTo(m2) < 0);
    }

    [Fact]
    public void Money_Zero_returns_zero_amount()
    {
        var zero = Money.Zero("USD");
        Assert.Equal(0m, zero.Amount);
        Assert.Equal("USD", zero.Currency);
    }
}
