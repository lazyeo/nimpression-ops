using FluentValidation;

namespace Nimpression.Application.Features.Fines.Commands.DisputeFine;

public sealed class DisputeFineCommandValidator : AbstractValidator<DisputeFineCommand>
{
    public DisputeFineCommandValidator()
    {
        RuleFor(x => x.FineId)
            .NotEmpty()
            .WithMessage("FineId cannot be empty.");

        RuleFor(x => x.ReviewNote)
            .NotEmpty()
            .WithMessage("Review note is mandatory when disputing a fine.");
    }
}
