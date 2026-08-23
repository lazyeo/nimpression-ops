using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Queries.GetVehiclesList;

/// <summary>
/// 车辆列表分页查询校验器。
/// </summary>
public sealed class GetVehiclesListQueryValidator : AbstractValidator<GetVehiclesListQuery>
{
    public GetVehiclesListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Status)
            .IsInEnum().When(x => x.Status.HasValue).WithMessage("Invalid vehicle status.");
    }
}
