using FluentValidation;

namespace Nimpression.Application.Features.Drivers.Commands.CreateDriver;

/// <summary>
/// 创建司机命令校验器。
/// </summary>
public sealed class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(100).WithMessage("Display name cannot exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.EmployeeNo)
            .NotEmpty().WithMessage("Employee number is required.")
            .MaximumLength(30).WithMessage("Employee number cannot exceed 30 characters.");

        RuleFor(x => x.LicenceClass)
            .NotEmpty().WithMessage("Licence class is required.")
            .MaximumLength(20).WithMessage("Licence class cannot exceed 20 characters.");

        RuleFor(x => x.HourlyRateAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Hourly rate must be non-negative.");

        RuleFor(x => x.PerTripRateAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Per trip rate must be non-negative.");

        RuleFor(x => x.PerKmRateAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Per km rate must be non-negative.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.");

        RuleFor(x => x.EmergencyContact)
            .NotEmpty().WithMessage("Emergency contact is required.");
    }
}
