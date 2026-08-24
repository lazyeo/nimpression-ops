using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Commands.RecordVehicleService;

/// <summary>
/// 记录车辆保养命令校验器。
/// </summary>
public sealed class RecordVehicleServiceCommandValidator : AbstractValidator<RecordVehicleServiceCommand>
{
    public RecordVehicleServiceCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Vehicle ID is required.");

        RuleFor(x => x.ServiceOdometerKm)
            .GreaterThanOrEqualTo(0m).WithMessage("Service odometer reading cannot be negative.");
    }
}
