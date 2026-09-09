using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Services;

/// <summary>Adult hourly minimum for the dates work is performed, not the eventual pay date.
/// Sources: employment.govt.nz/pay-and-hours/pay-and-wages/minimum-wage/previous-min-wage-rates
/// and employment.govt.nz/news-and-updates/minimum-wage-is-increasing-on-1-april-2026.
/// This policy does not apply reduced starting-out/training rates or infer eligibility for them.</summary>
public static class NzAdultMinimumWage
{
    public static decimal ForWorkDate(DateOnly workDate)
    {
        if (workDate < new DateOnly(2024, 4, 1) || workDate > new DateOnly(2027, 3, 31))
            throw new DomainValidationException("Minimum wage rules are only configured for work from 1 April 2024 through 31 March 2027.");
        if (workDate >= new DateOnly(2026, 4, 1)) return 23.95m;
        return workDate >= new DateOnly(2025, 4, 1) ? 23.50m : 23.15m;
    }

    public static Money ForPeriod(DateOnly startsOn, DateOnly endsOn, decimal? agreedMinimum = null)
    {
        if (endsOn < startsOn) throw new DomainValidationException("The pay period end must not precede its start.");
        var statutory = ForWorkDate(startsOn);
        if (statutory != ForWorkDate(endsOn))
            throw new DomainValidationException("Split this pay period at the minimum wage rate change before calculating payroll.");
        if (agreedMinimum.HasValue && agreedMinimum.Value < statutory)
            throw new DomainValidationException("The requested minimum hourly wage is below the statutory adult rate for this work period.");
        return new Money(agreedMinimum ?? statutory);
    }
}
