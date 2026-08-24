using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Commands.UpdateVehicle;

/// <summary>
/// 更新车辆命令校验器。
/// </summary>
public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Vehicle ID is required.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid vehicle status.");
    }
}
