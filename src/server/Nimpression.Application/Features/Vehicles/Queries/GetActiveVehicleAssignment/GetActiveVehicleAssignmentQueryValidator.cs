using FluentValidation;

namespace Nimpression.Application.Features.Vehicles.Queries.GetActiveVehicleAssignment;

/// <summary>
/// 获取当前生效中分派记录查询校验器。
/// </summary>
public sealed class GetActiveVehicleAssignmentQueryValidator : AbstractValidator<GetActiveVehicleAssignmentQuery>
{
    public GetActiveVehicleAssignmentQueryValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle ID is required.");
    }
}
