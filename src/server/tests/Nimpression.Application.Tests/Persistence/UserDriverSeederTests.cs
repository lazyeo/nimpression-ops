using FluentAssertions;
using Nimpression.Infrastructure.Persistence.Seed;

namespace Nimpression.Application.Tests.Persistence;

public sealed class UserDriverSeederTests
{
    [Fact]
    public void Generate_UsesReadableContactValuesInsideTheExistingWrapper()
    {
        var (_, drivers) = UserDriverSeeder.Generate();

        drivers.Should().HaveCount(10);
        drivers[0].PhoneEnc.Should().Be("ENC(+6421100000)");
        drivers[0].AddressEnc.Should().Be("ENC(10 Queen Street, Auckland)");
        drivers[0].EmergencyContactEnc.Should().Be("ENC(Emergency contact: +6421999900)");
        drivers[9].PhoneEnc.Should().Be("ENC(+6421100009)");
        drivers[9].AddressEnc.Should().Be("ENC(19 Queen Street, Auckland)");
        drivers[9].EmergencyContactEnc.Should().Be("ENC(Emergency contact: +6421999909)");
        drivers.Should().OnlyContain(driver =>
            driver.PhoneEnc.StartsWith("ENC(+", StringComparison.Ordinal) &&
            driver.AddressEnc.Contains(" Queen Street, Auckland)", StringComparison.Ordinal) &&
            driver.EmergencyContactEnc.StartsWith("ENC(Emergency contact: +", StringComparison.Ordinal));
    }
}
