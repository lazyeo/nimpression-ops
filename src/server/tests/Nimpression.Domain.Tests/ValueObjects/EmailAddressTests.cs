using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.ValueObjects;

public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("admin@nimpression.co.nz", "admin@nimpression.co.nz")]
    [InlineData("  Driver.One@Example.Com  ", "driver.one@example.com")]
    [InlineData("TEST.123+TAG@DOMAIN.ORG", "test.123+tag@domain.org")]
    public void EmailAddress_normalizes_to_lowercase_and_trims(string input, string expected)
    {
        var email = new EmailAddress(input);
        Assert.Equal(expected, email.Value);
        Assert.Equal(expected, email.ToString());
        Assert.Equal(expected, (string)email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plainaddress")]
    [InlineData("@missingusername.com")]
    [InlineData("username@.com")]
    [InlineData("username@domain")]
    public void EmailAddress_throws_on_invalid_format(string input)
    {
        Assert.Throws<DomainValidationException>(() => new EmailAddress(input));
    }

    [Fact]
    public void EmailAddress_comparisons_and_equality()
    {
        var e1 = new EmailAddress("a@test.com");
        var e2 = new EmailAddress("z@test.com");
        var e3 = new EmailAddress("A@TEST.COM");

        Assert.True(e1 < e2);
        Assert.True(e2 > e1);
        Assert.True(e1 <= e3);
        Assert.True(e1 >= e3);
        Assert.Equal(e1, e3);
    }
}
