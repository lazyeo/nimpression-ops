using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.Commands.FinalisePayPeriod;

public sealed record FinalisePayPeriodCommand(
    Guid PayPeriodId) : IRequest<Result<PayPeriodDto>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "PayPeriod";
    public Guid? AuditEntityId => PayPeriodId;
    public string AuditAction => "FinalisePayPeriod";
}

public sealed class FinalisePayPeriodCommandValidator : AbstractValidator<FinalisePayPeriodCommand>
{
    public FinalisePayPeriodCommandValidator()
    {
        RuleFor(x => x.PayPeriodId)
            .NotEmpty().WithMessage("Pay period ID is required.");
    }
}

public sealed class FinalisePayPeriodCommandHandler(
    Abstractions.IPayrollRepository payrollRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditSink auditSink,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<FinalisePayPeriodCommand, Result<PayPeriodDto>>
{
    public async Task<Result<PayPeriodDto>> Handle(FinalisePayPeriodCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            return Error.Forbidden("forbidden", "Only system administrators can finalise pay periods.");
        }

        var payPeriod = await payrollRepository.GetPayPeriodByIdAsync(request.PayPeriodId, cancellationToken);
        if (payPeriod is null)
        {
            return Error.NotFound("pay_period_not_found", $"Pay period with ID '{request.PayPeriodId}' was not found.");
        }

        if (payPeriod.Status == PayPeriodStatus.Finalised || payPeriod.Status == PayPeriodStatus.Paid)
        {
            return Error.Unprocessable(
                "already_finalised",
                $"Pay period is already in '{payPeriod.Status}' status.");
        }

        var payslips = await payrollRepository.GetPayslipsByPeriodIdAsync(payPeriod.Id, cancellationToken);
        if (payslips.Count == 0)
        {
            return Error.Unprocessable("no_payslips", "Cannot finalise a pay period without any calculated payslips.");
        }

        // F7.6: 里程来源优先级；里程差为负或 >1000km 拒绝定版
        foreach (var payslip in payslips)
        {
            var tasks = await payrollRepository.GetCompletedJobTasksForDriverAndPeriodAsync(
                payslip.DriverId,
                payPeriod.StartsOn,
                payPeriod.EndsOn,
                cancellationToken);

            foreach (var task in tasks)
            {
                if (task.EndOdometerKm.HasValue && task.StartOdometerKm.HasValue)
                {
                    if (task.EndOdometerKm.Value < task.StartOdometerKm.Value)
                    {
                        return Error.Unprocessable(
                            "invalid_odometer_negative",
                            $"Task {task.Ref} end odometer ({task.EndOdometerKm.Value.Value} km) is less than start odometer ({task.StartOdometerKm.Value.Value} km). Finalisation rejected.");
                    }

                    var diff = task.EndOdometerKm.Value - task.StartOdometerKm.Value;
                    if (diff.Value > 1000m)
                    {
                        return Error.Unprocessable(
                            "invalid_odometer_exceeded",
                            $"Task {task.Ref} odometer difference ({diff.Value} km) exceeds 1000 km. Finalisation rejected.");
                    }
                }
            }
        }

        var finalisedAt = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        foreach (var payslip in payslips)
        {
            if (!payslip.FinalisedAt.HasValue)
            {
                payslip.Finalise(finalisedAt);
            }
        }

        payPeriod.Finalise(finalisedAt);
        payrollRepository.UpdatePayPeriod(payPeriod);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditSink.RecordAsync(
            "PayPeriod",
            payPeriod.Id,
            "FinalisePayPeriod",
            $"{{\"status\":\"Calculating\"}}",
            $"{{\"status\":\"Finalised\",\"finalisedAt\":\"{finalisedAt:O}\",\"payslipCount\":{payslips.Count}}}",
            cancellationToken);

        return PayPeriodDto.FromEntity(payPeriod, payslips.Count);
    }
}
