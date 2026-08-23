using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Payroll;

/// <summary>
/// 薪资周期聚合根。管理双周薪期起止日期、计算状态与定版流转。
/// </summary>
public sealed class PayPeriod : AggregateRoot
{
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public PayPeriodStatus Status { get; private set; }
    public DateTimeOffset? FinalisedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }

    private PayPeriod()
    {
    }

    public PayPeriod(
        Guid id,
        DateOnly startsOn,
        DateOnly endsOn,
        PayPeriodStatus status = PayPeriodStatus.Open) : base(id)
    {
        if (endsOn < startsOn)
        {
            throw new DomainValidationException(
                $"PayPeriod EndsOn ({endsOn:yyyy-MM-dd}) cannot be earlier than StartsOn ({startsOn:yyyy-MM-dd}).");
        }

        StartsOn = startsOn;
        EndsOn = endsOn;
        Status = status;
    }

    public void SetStatus(PayPeriodStatus status)
    {
        Status = status;
    }

    public void Finalise(DateTimeOffset finalisedAt)
    {
        if (Status != PayPeriodStatus.Calculating && Status != PayPeriodStatus.Open)
        {
            throw new DomainValidationException($"Cannot finalise pay period in '{Status}' status.");
        }

        Status = PayPeriodStatus.Finalised;
        FinalisedAt = finalisedAt;
    }

    public void MarkPaid(DateTimeOffset paidAt)
    {
        if (Status != PayPeriodStatus.Finalised)
        {
            throw new DomainValidationException($"Cannot mark unpaid period as paid from '{Status}' status.");
        }

        Status = PayPeriodStatus.Paid;
        PaidAt = paidAt;
    }

    public bool Contains(DateOnly date) => date >= StartsOn && date <= EndsOn;
}
