using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Commands.UpdateVehicleStatus;

/// <summary>
/// 更新车辆状态命令校验器。
/// </summary>
public sealed class UpdateVehicleStatusCommandValidator : AbstractValidator<UpdateVehicleStatusCommand>
{
    public UpdateVehicleStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Vehicle ID is required.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid vehicle status.");
    }
}
