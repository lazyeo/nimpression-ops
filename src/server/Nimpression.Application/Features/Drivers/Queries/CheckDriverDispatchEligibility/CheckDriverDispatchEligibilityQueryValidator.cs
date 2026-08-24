using FluentValidation;

namespace Nimpression.Application.Features.Drivers.Queries.CheckDriverDispatchEligibility;

/// <summary>
/// 检查司机派单资格查询校验器。
/// </summary>
public sealed class CheckDriverDispatchEligibilityQueryValidator : AbstractValidator<CheckDriverDispatchEligibilityQuery>
{
    public CheckDriverDispatchEligibilityQueryValidator()
    {
        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("DriverId is required.");
    }
}
