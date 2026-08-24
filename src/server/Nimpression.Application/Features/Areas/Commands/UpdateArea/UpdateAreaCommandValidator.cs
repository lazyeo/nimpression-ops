using FluentValidation;

namespace Nimpression.Application.Features.Areas.Commands.UpdateArea;

public sealed class UpdateAreaCommandValidator : AbstractValidator<UpdateAreaCommand>
{
    public UpdateAreaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Area ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Area name is required.")
            .MaximumLength(100).WithMessage("Area name must not exceed 100 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Area code is required.")
            .MaximumLength(30).WithMessage("Area code must not exceed 30 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Area description must not exceed 500 characters.");
    }
}
