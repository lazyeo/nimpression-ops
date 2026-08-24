using FluentValidation;

namespace Nimpression.Application.Features.Dispatch.Commands.StartJobTask;

public sealed class StartJobTaskCommandValidator : AbstractValidator<StartJobTaskCommand>
{
    public StartJobTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");

        RuleFor(x => x.StartOdometerKm)
            .GreaterThanOrEqualTo(0).When(x => x.StartOdometerKm.HasValue)
            .WithMessage("Start odometer reading cannot be negative.");
    }
}
