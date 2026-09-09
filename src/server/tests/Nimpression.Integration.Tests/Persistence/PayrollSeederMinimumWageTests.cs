using Nimpression.Infrastructure.Persistence.Seed;
using Xunit;

namespace Nimpression.Integration.Tests.Persistence;

// Pure seed generation regression; no fixture/database is needed.
public class PayrollSeederMinimumWageTests
{
    [Fact]
    public void Generated2026Periods_UseCurrentAdultMinimumOnTopUpLines()
    {
        var (_, drivers) = UserDriverSeeder.Generate();
        var (periods, slips) = PayrollSeeder.Generate(drivers);
        Assert.All(periods, period => Assert.True(period.StartsOn >= new DateOnly(2026, 4, 1)));
        var topUps = slips.SelectMany(slip => slip.Lines).Where(line => line.Kind == "MinimumWageTopUp").ToList();
        Assert.NotEmpty(topUps);
        Assert.All(topUps, line => Assert.Equal(23.95m, line.Rate.Amount));
        Assert.All(slips.Where(slip => slip.MinimumWageTopUp), slip =>
            Assert.Equal((slip.OrdinaryHours.Value + slip.OvertimeHours.Value + slip.HolidayHours.Value) * 23.95m, slip.GrossPay.Amount));
    }
}
