using FluentValidation;

namespace Nimpression.Application.Features.Drivers.Commands.DeactivateDriver;

/// <summary>
/// 停用司机命令校验器。
/// </summary>
public sealed class DeactivateDriverCommandValidator : AbstractValidator<DeactivateDriverCommand>
{
    public DeactivateDriverCommandValidator()
    {
        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("DriverId is required.");
    }
}
