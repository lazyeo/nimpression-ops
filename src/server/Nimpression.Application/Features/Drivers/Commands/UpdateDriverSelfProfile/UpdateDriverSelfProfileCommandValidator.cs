using FluentValidation;

namespace Nimpression.Application.Features.Drivers.Commands.UpdateDriverSelfProfile;

/// <summary>
/// 司机个人资料自助修改命令校验器。
/// </summary>
public sealed class UpdateDriverSelfProfileCommandValidator : AbstractValidator<UpdateDriverSelfProfileCommand>
{
    public UpdateDriverSelfProfileCommandValidator()
    {
        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("DriverId is required.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.");

        RuleFor(x => x.EmergencyContact)
            .NotEmpty().WithMessage("Emergency contact is required.");

        RuleFor(x => x.Locale)
            .NotEmpty().WithMessage("Locale is required.")
            .MaximumLength(10).WithMessage("Locale cannot exceed 10 characters.");
    }
}
