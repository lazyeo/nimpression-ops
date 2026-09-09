using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Services.Payroll;

namespace Nimpression.Infrastructure.Persistence.Seed;

/// <summary>Explicit synthetic demo declarations, never inferred real employee tax settings.</summary>
public static class DemoPayrollSettlement
{
    public static PayslipSettlementSnapshot Create(decimal gross, PayPeriod period, DateTimeOffset calculatedAt)
    {
        var payDate = period.PaidAt.HasValue ? NzTimeZone.ToNzDateOnly(period.PaidAt.Value) : period.EndsOn.AddDays(3);
        var settings = new SettlementRequest(payDate, SettlementPayFrequency.Fortnightly, gross, SettlementWorkerType.Employee,
            new EmployeeSettlementProfile("M", new(0.035m, true, false), new(0.035m, true, false, 0.30m, true),
                new(HolidayPayMode.OrdinaryAccrual, false, false)), null);
        var result = PayrollSettlementCalculator.Calculate(settings);
        if (!result.IsSuccess)
            throw new InvalidOperationException("The fixed demo payroll declarations are outside the supported settlement rules.");
        return new PayslipSettlementSnapshot(settings, result.Calculation!, PayrollSettlementCalculator.RulesVersion, calculatedAt, IsDemo: true);
    }
}
