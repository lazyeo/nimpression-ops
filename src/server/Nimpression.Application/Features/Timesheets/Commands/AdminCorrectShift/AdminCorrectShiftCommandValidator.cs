using FluentValidation;

namespace Nimpression.Application.Features.Timesheets.Commands.AdminCorrectShift;

/// <summary>
/// 管理员更正打卡记录命令校验器。
/// </summary>
public sealed class AdminCorrectShiftCommandValidator : AbstractValidator<AdminCorrectShiftCommand>
{
    public AdminCorrectShiftCommandValidator()
    {
        RuleFor(x => x.ShiftId)
            .NotEmpty()
            .WithMessage("ShiftId cannot be empty.");

        RuleFor(x => x.NewBreakMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Break minutes cannot be negative.");
    }
}
