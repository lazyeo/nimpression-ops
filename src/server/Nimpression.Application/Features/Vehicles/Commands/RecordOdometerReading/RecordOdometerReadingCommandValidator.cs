using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Commands.RecordOdometerReading;

/// <summary>
/// 上报车辆里程读数命令校验器。
/// </summary>
public sealed class RecordOdometerReadingCommandValidator : AbstractValidator<RecordOdometerReadingCommand>
{
    public RecordOdometerReadingCommandValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle ID is required.");

        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("Driver ID is required.");

        RuleFor(x => x.ReadingKm)
            .GreaterThanOrEqualTo(0m).WithMessage("Odometer reading cannot be negative.");

        RuleFor(x => x.PhotoKey)
            .MaximumLength(200).WithMessage("Photo key cannot exceed 200 characters.");

        RuleFor(x => x.Source)
            .NotEmpty().WithMessage("Source is required.")
            .MaximumLength(50).WithMessage("Source cannot exceed 50 characters.");
    }
}
