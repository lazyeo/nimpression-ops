using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Commands.CreateVehicle;

/// <summary>
/// 创建车辆命令校验器。
/// </summary>
public sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.Rego)
            .NotEmpty().WithMessage("Vehicle registration (Rego) is required.")
            .Must(rego =>
            {
                if (string.IsNullOrWhiteSpace(rego))
                {
                    return false;
                }

                var normalized = rego.Trim().Replace(" ", string.Empty).ToUpperInvariant();
                return normalized.Length >= 1 && normalized.Length <= 6 && normalized.All(char.IsLetterOrDigit);
            }).WithMessage("Invalid NZ registration plate format. Must be 1-6 alphanumeric characters.");

        RuleFor(x => x.Make)
            .NotEmpty().WithMessage("Vehicle make is required.")
            .MaximumLength(50).WithMessage("Vehicle make cannot exceed 50 characters.");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Vehicle model is required.")
            .MaximumLength(50).WithMessage("Vehicle model cannot exceed 50 characters.");

        RuleFor(x => x.Year)
            .InclusiveBetween(1900, DateTime.UtcNow.Year + 2)
            .WithMessage($"Vehicle year must be between 1900 and {DateTime.UtcNow.Year + 2}.");

        RuleFor(x => x.VinEnc)
            .NotEmpty().WithMessage("VIN is required.")
            .MaximumLength(500).WithMessage("VIN cannot exceed 500 characters.");

        RuleFor(x => x.OdometerKm)
            .GreaterThanOrEqualTo(0m).WithMessage("Odometer reading cannot be negative.");

        RuleFor(x => x.ServiceIntervalKm)
            .GreaterThan(0m).WithMessage("Service interval must be greater than zero.");

        RuleFor(x => x.LastServiceOdometerKm)
            .Must((cmd, lastSvc) => !lastSvc.HasValue || lastSvc.Value <= cmd.OdometerKm)
            .WithMessage("Last service odometer cannot exceed current odometer reading.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid vehicle status.");
    }
}
