using FluentValidation;

namespace Nimpression.Application.Features.Dispatch.Commands.CancelJobTask;

public sealed class CancelJobTaskCommandValidator : AbstractValidator<CancelJobTaskCommand>
{
    public CancelJobTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Cancellation reason is required.")
            .MaximumLength(500).WithMessage("Cancellation reason cannot exceed 500 characters.");
    }
}
