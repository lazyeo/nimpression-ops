using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Queries.GetVehicleById;

/// <summary>
/// 按 ID 获取车辆详情查询校验器。
/// </summary>
public sealed class GetVehicleByIdQueryValidator : AbstractValidator<GetVehicleByIdQuery>
{
    public GetVehicleByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Vehicle ID is required.");
    }
}
