using FluentValidation;

namespace Nimpression.Application.Features.Fines.Commands.SubmitFine;

public sealed class SubmitFineCommandValidator : AbstractValidator<SubmitFineCommand>
{
    public SubmitFineCommandValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty()
            .WithMessage("VehicleId is mandatory.");

        RuleFor(x => x.Authority)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Authority cannot be empty or exceed 100 characters.");

        RuleFor(x => x.Reference)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Reference cannot be empty or exceed 100 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Fine amount must be greater than 0.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Reason cannot be empty or exceed 500 characters.");
    }
}
