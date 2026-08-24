using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Commands.AssignVehicle;

/// <summary>
/// 车辆指派命令校验器。
/// </summary>
public sealed class AssignVehicleCommandValidator : AbstractValidator<AssignVehicleCommand>
{
    public AssignVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle ID is required.");

        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("Driver ID is required.");
    }
}
