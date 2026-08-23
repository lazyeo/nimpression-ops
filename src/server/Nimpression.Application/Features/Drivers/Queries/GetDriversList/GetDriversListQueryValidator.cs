using FluentValidation;

namespace Nimpression.Application.Features.Drivers.Queries.GetDriversList;

/// <summary>
/// 司机列表分页查询校验器。
/// </summary>
public sealed class GetDriversListQueryValidator : AbstractValidator<GetDriversListQuery>
{
    public GetDriversListQueryValidator()
    {
        RuleFor(x => x.Filter.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}
