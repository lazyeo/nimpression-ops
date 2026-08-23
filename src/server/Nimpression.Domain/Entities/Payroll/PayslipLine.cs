using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Payroll;

/// <summary>
/// 工资单明细项实体。同时保留工时与趟次双口径的所有计算行供司机核对。
/// </summary>
public sealed class PayslipLine : Entity
{
    public Guid PayslipId { get; private set; }
    public PayBasis Basis { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public WorkHours? Hours { get; private set; }
    public Kilometres? Distance { get; private set; }
    public int? Qty { get; private set; }
    public Money Rate { get; private set; }
    public Money Amount { get; private set; }

    private PayslipLine()
    {
    }

    public PayslipLine(
        Guid id,
        Guid payslipId,
        PayBasis basis,
        string kind,
        string description,
        Money rate,
        Money amount,
        WorkHours? hours = null,
        Kilometres? distance = null,
        int? qty = null) : base(id)
    {
        if (payslipId == Guid.Empty)
        {
            throw new DomainValidationException("PayslipId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new DomainValidationException("PayslipLine kind cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainValidationException("PayslipLine description cannot be empty.");
        }

        PayslipId = payslipId;
        Basis = basis;
        Kind = kind.Trim();
        Description = description.Trim();
        Rate = rate;
        Amount = amount;
        Hours = hours;
        Distance = distance;
        Qty = qty;
    }
}
