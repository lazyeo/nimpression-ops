using FluentValidation;

namespace Nimpression.Application.Features.Fines.Commands.WaiveFine;

public sealed class WaiveFineCommandValidator : AbstractValidator<WaiveFineCommand>
{
    public WaiveFineCommandValidator()
    {
        RuleFor(x => x.FineId)
            .NotEmpty()
            .WithMessage("FineId cannot be empty.");

        RuleFor(x => x.ReviewNote)
            .NotEmpty()
            .WithMessage("Review note is mandatory when waiving a fine.");
    }
}
