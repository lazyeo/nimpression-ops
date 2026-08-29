using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Application.Features.Areas.Commands.EndAreaAssignment;

/// <summary>
/// 结束司机区域分配命令处理器。
/// </summary>
public sealed class EndAreaAssignmentCommandHandler(
    IAreaRepository areaRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<EndAreaAssignmentCommand, Result>
{
    public async Task<Result> Handle(EndAreaAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await areaRepository.GetAssignmentByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Error.NotFound("area_assignment_not_found", $"Area assignment '{request.AssignmentId}' was not found.");
        }

        try
        {
            assignment.EndAssignment(request.EffectiveTo);
        }
        catch (DomainValidationException ex)
        {
            return Error.Unprocessable("invalid_effective_date", ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
