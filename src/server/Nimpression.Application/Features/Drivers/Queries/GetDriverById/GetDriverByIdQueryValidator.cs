using FluentValidation;

namespace Nimpression.Application.Features.Drivers.Queries.GetDriverById;

/// <summary>
/// 按 ID 获取司机详情查询校验器。
/// </summary>
public sealed class GetDriverByIdQueryValidator : AbstractValidator<GetDriverByIdQuery>
{
    public GetDriverByIdQueryValidator()
    {
        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("DriverId is required.");
    }
}
