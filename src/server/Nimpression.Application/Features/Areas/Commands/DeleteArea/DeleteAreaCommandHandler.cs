using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Application.Features.Areas.Common;

namespace Nimpression.Application.Features.Areas.Commands.DeleteArea;

/// <summary>
/// 删除运营区域命令处理器。
/// F4.1: 删除有生效中分配的区域返回 409 Conflict。
/// </summary>
public sealed class DeleteAreaCommandHandler(
    IAreaRepository areaRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<DeleteAreaCommand, Result>
{
    public async Task<Result> Handle(DeleteAreaCommand request, CancellationToken cancellationToken)
    {
        var area = await areaRepository.GetByIdAsync(request.Id, cancellationToken);
        if (area is null)
        {
            return Error.NotFound("area_not_found", $"Area with ID '{request.Id}' was not found.");
        }

        var hasActiveAssignments = await areaRepository.HasActiveAssignmentsAsync(
            request.Id,
            dateTimeProvider.NzToday,
            cancellationToken);

        if (hasActiveAssignments)
        {
            return Error.Conflict("area_has_active_assignments", "Cannot delete area with active assignments.");
        }

        try
        {
            areaRepository.DeleteArea(area);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DbExceptionHelper.IsForeignKeyViolation(ex))
        {
            return Error.Conflict("area_in_use", "Cannot delete area that is referenced by existing records.");
        }

        return Result.Success();
    }
}
