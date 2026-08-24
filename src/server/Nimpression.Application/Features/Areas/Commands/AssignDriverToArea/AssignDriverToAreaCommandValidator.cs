using FluentValidation;

namespace Nimpression.Application.Features.Areas.Commands.AssignDriverToArea;

public sealed class AssignDriverToAreaCommandValidator : AbstractValidator<AssignDriverToAreaCommand>
{
    public AssignDriverToAreaCommandValidator()
    {
        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("Driver ID is required.");

        RuleFor(x => x.AreaId)
            .NotEmpty().WithMessage("Area ID is required.");

        RuleFor(x => x.EffectiveFrom)
            .NotEmpty().WithMessage("EffectiveFrom date is required.");

        RuleFor(x => x)
            .Must(x => !x.EffectiveTo.HasValue || x.EffectiveTo.Value >= x.EffectiveFrom)
            .WithMessage("EffectiveTo date cannot be earlier than EffectiveFrom date.");
    }
}
