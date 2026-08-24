using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Queries.GetOdometerReadings;

/// <summary>
/// 车辆里程读数历史查询校验器。
/// </summary>
public sealed class GetOdometerReadingsQueryValidator : AbstractValidator<GetOdometerReadingsQuery>
{
    public GetOdometerReadingsQueryValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle ID is required.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 200).WithMessage("Limit must be between 1 and 200.");
    }
}
