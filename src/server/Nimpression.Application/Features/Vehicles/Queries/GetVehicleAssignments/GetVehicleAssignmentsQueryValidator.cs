using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Queries.GetVehicleAssignments;

/// <summary>
/// 车辆分派历史查询校验器。
/// </summary>
public sealed class GetVehicleAssignmentsQueryValidator : AbstractValidator<GetVehicleAssignmentsQuery>
{
    public GetVehicleAssignmentsQueryValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle ID is required.");
    }
}
