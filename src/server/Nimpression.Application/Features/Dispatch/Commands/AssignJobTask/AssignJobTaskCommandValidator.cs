using FluentValidation;

namespace Nimpression.Application.Features.Dispatch.Commands.AssignJobTask;

public sealed class AssignJobTaskCommandValidator : AbstractValidator<AssignJobTaskCommand>
{
    public AssignJobTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("Task ID is required.");

        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("Driver ID is required.");

        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle ID is required.");
    }
}
