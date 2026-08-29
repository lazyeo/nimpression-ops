using FluentValidation;

namespace Nimpression.Application.Features.Dispatch.Commands.CreateJobTask;

public sealed class CreateJobTaskCommandValidator : AbstractValidator<CreateJobTaskCommand>
{
    public CreateJobTaskCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Task title is required.")
            .MaximumLength(200).WithMessage("Task title cannot exceed 200 characters.");

        RuleFor(x => x.AreaId)
            .NotEmpty().WithMessage("Area ID is required.");

        RuleFor(x => x.ScheduledFor)
            .NotEmpty().WithMessage("Scheduled date/time is required.");

        RuleFor(x => x.PlannedDistanceKm)
            .GreaterThan(0).When(x => x.PlannedDistanceKm.HasValue)
            .WithMessage("Planned distance must be greater than zero.");

        RuleFor(x => x)
            .Must(x => (x.DriverId.HasValue && x.VehicleId.HasValue) || (!x.DriverId.HasValue && !x.VehicleId.HasValue))
            .WithMessage("Both Driver and Vehicle must be provided together when assigning a task during creation.");
    }
}
