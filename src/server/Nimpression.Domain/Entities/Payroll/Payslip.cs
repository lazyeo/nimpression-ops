using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Payroll;

/// <summary>
/// 工资单聚合根。同时保留工时与趟次双套口径计算结果、快照费率与定版状态。
/// </summary>
public sealed class Payslip : AggregateRoot
{
    private readonly List<PayslipLine> _lines = [];

    public Guid PayPeriodId { get; private set; }
    public Guid DriverId { get; private set; }

    // 工时口径
    public WorkHours OrdinaryHours { get; private set; }
    public WorkHours OvertimeHours { get; private set; }
    public WorkHours HolidayHours { get; private set; }
    public Money HourlyRateSnapshot { get; private set; }
    public Money HoursBasedGross { get; private set; }

    // 趟次口径
    public int CompletedTripCount { get; private set; }
    public Kilometres TotalDistanceKm { get; private set; }
    public Money PerTripRateSnapshot { get; private set; }
    public Money PerKmRateSnapshot { get; private set; }
    public Money TripBasedGross { get; private set; }

    // 结算
    public PayBasis BasisUsed { get; private set; }
    public Money GrossPay { get; private set; }
    public bool MinimumWageTopUp { get; private set; }
    public DateTimeOffset CalculatedAt { get; private set; }
    public DateTimeOffset? FinalisedAt { get; private set; }

    public IReadOnlyCollection<PayslipLine> Lines => _lines.AsReadOnly();

    private Payslip()
    {
    }

    public Payslip(
        Guid id,
        Guid payPeriodId,
        Guid driverId,
        WorkHours ordinaryHours,
        WorkHours overtimeHours,
        WorkHours holidayHours,
        Money hourlyRateSnapshot,
        Money hoursBasedGross,
        int completedTripCount,
        Kilometres totalDistanceKm,
        Money perTripRateSnapshot,
        Money perKmRateSnapshot,
        Money tripBasedGross,
        PayBasis basisUsed,
        Money grossPay,
        bool minimumWageTopUp,
        DateTimeOffset calculatedAt) : base(id)
    {
        if (payPeriodId == Guid.Empty)
        {
            throw new DomainValidationException("PayPeriodId cannot be empty.");
        }

        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("DriverId cannot be empty.");
        }

        if (completedTripCount < 0)
        {
            throw new DomainValidationException("Completed trip count cannot be negative.");
        }

        PayPeriodId = payPeriodId;
        DriverId = driverId;
        OrdinaryHours = ordinaryHours;
        OvertimeHours = overtimeHours;
        HolidayHours = holidayHours;
        HourlyRateSnapshot = hourlyRateSnapshot;
        HoursBasedGross = hoursBasedGross;
        CompletedTripCount = completedTripCount;
        TotalDistanceKm = totalDistanceKm;
        PerTripRateSnapshot = perTripRateSnapshot;
        PerKmRateSnapshot = perKmRateSnapshot;
        TripBasedGross = tripBasedGross;
        BasisUsed = basisUsed;
        GrossPay = grossPay;
        MinimumWageTopUp = minimumWageTopUp;
        CalculatedAt = calculatedAt;
    }

    public void AddLine(PayslipLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (FinalisedAt.HasValue)
        {
            throw new DomainValidationException("Cannot add lines to a finalised payslip.");
        }

        if (line.PayslipId == Guid.Empty && Id != Guid.Empty)
        {
            line.AssignToPayslip(Id);
        }

        _lines.Add(line);
    }

    public void AddLines(IEnumerable<PayslipLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        foreach (var line in lines)
        {
            AddLine(line);
        }
    }

    public void Finalise(DateTimeOffset finalisedAt)
    {
        if (FinalisedAt.HasValue)
        {
            throw new DomainValidationException("Payslip is already finalised.");
        }

        FinalisedAt = finalisedAt;
        AddDomainEvent(new PayslipFinalised(Id, PayPeriodId, DriverId, GrossPay, finalisedAt));
    }
}
