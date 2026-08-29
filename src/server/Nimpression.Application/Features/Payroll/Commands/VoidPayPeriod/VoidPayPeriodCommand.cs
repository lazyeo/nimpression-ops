using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.Commands.VoidPayPeriod;

public sealed record VoidPayPeriodCommand(
    Guid PayPeriodId,
    string Reason) : IRequest<Result<PayPeriodDto>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "PayPeriod";
    public Guid? AuditEntityId => PayPeriodId;
    public string AuditAction => "VoidPayPeriod";
}

public sealed class VoidPayPeriodCommandValidator : AbstractValidator<VoidPayPeriodCommand>
{
    public VoidPayPeriodCommandValidator()
    {
        RuleFor(x => x.PayPeriodId)
            .NotEmpty().WithMessage("Pay period ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Void reason is mandatory.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}

public sealed class VoidPayPeriodCommandHandler(
    Abstractions.IPayrollRepository payrollRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditSink auditSink) : IRequestHandler<VoidPayPeriodCommand, Result<PayPeriodDto>>
{
    public async Task<Result<PayPeriodDto>> Handle(VoidPayPeriodCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            return Error.Forbidden("forbidden", "Only system administrators can void/reopen pay periods.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Unprocessable("reason_required", "Reason for voiding pay period is mandatory.");
        }

        var payPeriod = await payrollRepository.GetPayPeriodByIdAsync(request.PayPeriodId, cancellationToken);
        if (payPeriod is null)
        {
            return Error.NotFound("pay_period_not_found", $"Pay period with ID '{request.PayPeriodId}' was not found.");
        }

        if (payPeriod.Status == PayPeriodStatus.Paid)
        {
            return Error.Unprocessable("period_already_paid", "Cannot void a pay period that has already been paid.");
        }

        var payslips = await payrollRepository.GetPayslipsByPeriodIdAsync(payPeriod.Id, cancellationToken);
        var oldStatus = payPeriod.Status.ToString();

        // 移除所有旧工资单并重置周期状态为 Open
        payrollRepository.RemovePayslips(payslips);
        payPeriod.SetStatus(PayPeriodStatus.Open);
        payrollRepository.UpdatePayPeriod(payPeriod);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // F7.8 验收标准：只能作废重开，均写审计
        await auditSink.RecordAsync(
            "PayPeriod",
            payPeriod.Id,
            "VoidPayPeriod",
            $"{{\"status\":\"{oldStatus}\",\"payslipCount\":{payslips.Count}}}",
            $"{{\"status\":\"Open\",\"reason\":\"{request.Reason.Trim()}\"}}",
            cancellationToken);

        return PayPeriodDto.FromEntity(payPeriod, 0);
    }
}
