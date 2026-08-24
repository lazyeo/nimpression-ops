using FluentValidation;

namespace Nimpression.Application.Features.Areas.Commands.EndAreaAssignment;

public sealed class EndAreaAssignmentCommandValidator : AbstractValidator<EndAreaAssignmentCommand>
{
    public EndAreaAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId)
            .NotEmpty().WithMessage("Assignment ID is required.");

        RuleFor(x => x.EffectiveTo)
            .NotEmpty().WithMessage("EffectiveTo date is required.");
    }
}
