using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.Commands.CreatePayPeriod;

public sealed record CreatePayPeriodCommand(
    DateOnly StartsOn,
    DateOnly? EndsOn = null) : IRequest<Result<PayPeriodDto>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "PayPeriod";
    public Guid? AuditEntityId => null;
    public string AuditAction => "CreatePayPeriod";
}

public sealed class CreatePayPeriodCommandValidator : AbstractValidator<CreatePayPeriodCommand>
{
    public CreatePayPeriodCommandValidator()
    {
        RuleFor(x => x.StartsOn)
            .NotEmpty().WithMessage("Pay period start date is required.");

        RuleFor(x => x)
            .Must(x => !x.EndsOn.HasValue || x.EndsOn.Value >= x.StartsOn)
            .WithMessage("Pay period end date cannot be earlier than start date.");
    }
}

public sealed class CreatePayPeriodCommandHandler(
    Abstractions.IPayrollRepository payrollRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditSink auditSink) : IRequestHandler<CreatePayPeriodCommand, Result<PayPeriodDto>>
{
    public async Task<Result<PayPeriodDto>> Handle(CreatePayPeriodCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Dispatcher)
        {
            return Error.Forbidden("forbidden", "Only administrators or dispatchers can create pay periods.");
        }

        var startsOn = request.StartsOn;
        var endsOn = request.EndsOn ?? startsOn.AddDays(13); // 14-day fortnightly pay period

        // F7.7: 双周薪期不可重叠
        var hasOverlap = await payrollRepository.HasOverlappingPayPeriodAsync(startsOn, endsOn, null, cancellationToken);
        if (hasOverlap)
        {
            return Error.Conflict("pay_period_overlap", $"Pay period {startsOn:yyyy-MM-dd} to {endsOn:yyyy-MM-dd} overlaps with an existing pay period.");
        }

        var payPeriod = new PayPeriod(
            id: Guid.NewGuid(),
            startsOn: startsOn,
            endsOn: endsOn,
            status: PayPeriodStatus.Open);

        await payrollRepository.AddPayPeriodAsync(payPeriod, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditSink.RecordAsync(
            "PayPeriod",
            payPeriod.Id,
            "CreatePayPeriod",
            null,
            $"{{\"startsOn\":\"{startsOn:yyyy-MM-dd}\",\"endsOn\":\"{endsOn:yyyy-MM-dd}\",\"status\":\"{payPeriod.Status}\"}}",
            cancellationToken);

        return PayPeriodDto.FromEntity(payPeriod, 0);
    }
}
