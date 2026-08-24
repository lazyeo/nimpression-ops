using FluentValidation;

namespace Nimpression.Application.Features.Timesheets.Commands.ClockOut;

/// <summary>
/// 下班打卡命令校验器。
/// </summary>
public sealed class ClockOutCommandValidator : AbstractValidator<ClockOutCommand>
{
    public ClockOutCommandValidator()
    {
        RuleFor(x => x.BreakMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Break minutes cannot be negative.");

        When(x => x.Latitude.HasValue, () =>
        {
            RuleFor(x => x.Latitude!.Value)
                .InclusiveBetween(-90m, 90m)
                .WithMessage("Latitude must be between -90 and 90.");
        });

        When(x => x.Longitude.HasValue, () =>
        {
            RuleFor(x => x.Longitude!.Value)
                .InclusiveBetween(-180m, 180m)
                .WithMessage("Longitude must be between -180 and 180.");
        });
    }
}
