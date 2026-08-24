using FluentValidation;

namespace Nimpression.Application.Features.Dispatch.Commands.CompleteJobTask;

public sealed class CompleteJobTaskCommandValidator : AbstractValidator<CompleteJobTaskCommand>
{
    public CompleteJobTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");

        RuleFor(x => x.ActualDistanceKm)
            .GreaterThanOrEqualTo(0).When(x => x.ActualDistanceKm.HasValue)
            .WithMessage("Actual distance cannot be negative.");

        RuleFor(x => x.EndOdometerKm)
            .GreaterThanOrEqualTo(0).When(x => x.EndOdometerKm.HasValue)
            .WithMessage("End odometer reading cannot be negative.");
    }
}
