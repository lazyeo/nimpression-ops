using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Commands.ReleaseVehicleAssignment;

/// <summary>
/// 释放车辆分派命令校验器。
/// </summary>
public sealed class ReleaseVehicleAssignmentCommandValidator : AbstractValidator<ReleaseVehicleAssignmentCommand>
{
    public ReleaseVehicleAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId)
            .NotEmpty().WithMessage("Assignment ID is required.");
    }
}
