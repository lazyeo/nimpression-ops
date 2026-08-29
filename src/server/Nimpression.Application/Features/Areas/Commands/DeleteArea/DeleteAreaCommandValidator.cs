using FluentValidation;

namespace Nimpression.Application.Features.Areas.Commands.DeleteArea;

public sealed class DeleteAreaCommandValidator : AbstractValidator<DeleteAreaCommand>
{
    public DeleteAreaCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Area ID is required.");
    }
}
