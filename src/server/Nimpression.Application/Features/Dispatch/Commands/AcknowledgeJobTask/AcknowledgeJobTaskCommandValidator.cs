using FluentValidation;

namespace Nimpression.Application.Features.Dispatch.Commands.AcknowledgeJobTask;

public sealed class AcknowledgeJobTaskCommandValidator : AbstractValidator<AcknowledgeJobTaskCommand>
{
    public AcknowledgeJobTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");
    }
}
