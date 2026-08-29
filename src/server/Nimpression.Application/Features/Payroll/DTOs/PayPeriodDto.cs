using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.DTOs;

public sealed record PayPeriodDto(
    Guid Id,
    DateOnly StartsOn,
    DateOnly EndsOn,
    PayPeriodStatus Status,
    DateTimeOffset? FinalisedAt,
    DateTimeOffset? PaidAt,
    int PayslipCount)
{
    public static PayPeriodDto FromEntity(PayPeriod period, int payslipCount = 0) =>
        new(
            period.Id,
            period.StartsOn,
            period.EndsOn,
            period.Status,
            period.FinalisedAt,
            period.PaidAt,
            payslipCount);
}

public sealed record PayPeriodFilter(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    PayPeriodStatus? Status = null,
    int Page = 1,
    int PageSize = 20);

public sealed record DriverPayslipsFilter(
    Guid DriverId,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Page = 1,
    int PageSize = 20);
