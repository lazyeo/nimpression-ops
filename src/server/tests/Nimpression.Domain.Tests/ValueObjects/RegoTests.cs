using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.ValueObjects;

public sealed class RegoTests
{
    [Theory]
    [InlineData("ABC123", "ABC123")]
    [InlineData("abc123", "ABC123")]
    [InlineData(" abc 123 ", "ABC123")]
    [InlineData("a1", "A1")]
    [InlineData("123456", "123456")]
    public void Rego_normalizes_to_uppercase_and_removes_spaces(string input, string expected)
    {
        var rego = new Rego(input);
        Assert.Equal(expected, rego.Value);
        Assert.Equal(expected, rego.ToString());
        Assert.Equal(expected, (string)rego);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOOLONG7")]
    [InlineData("AB@123")]
    [InlineData("AB-123")]
    public void Rego_throws_on_invalid_format(string input)
    {
        Assert.Throws<DomainValidationException>(() => new Rego(input));
    }

    [Fact]
    public void Rego_comparisons_and_equality()
    {
        var r1 = new Rego("AAA111");
        var r2 = new Rego("ZZZ999");
        var r3 = new Rego("aaa 111");

        Assert.True(r1 < r2);
        Assert.True(r2 > r1);
        Assert.True(r1 <= r3);
        Assert.True(r1 >= r3);
        Assert.Equal(r1, r3);
    }
}
