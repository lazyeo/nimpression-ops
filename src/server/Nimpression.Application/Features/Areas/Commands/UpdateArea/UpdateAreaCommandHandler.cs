using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Application.Features.Areas.Common;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Application.Features.Areas.Commands.UpdateArea;

/// <summary>
/// 更新运营区域命令处理器。
/// </summary>
public sealed class UpdateAreaCommandHandler(
    IAreaRepository areaRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateAreaCommand, Result>
{
    public async Task<Result> Handle(UpdateAreaCommand request, CancellationToken cancellationToken)
    {
        var area = await areaRepository.GetByIdAsync(request.Id, cancellationToken);
        if (area is null)
        {
            return Error.NotFound("area_not_found", $"Area with ID '{request.Id}' was not found.");
        }

        try
        {
            area.UpdateDetails(request.Name, request.Code, request.Description, request.GeoJson);
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_area_data", ex.Message);
        }

        if (request.IsActive && !area.IsActive)
        {
            area.Activate();
        }
        else if (!request.IsActive && area.IsActive)
        {
            area.Deactivate();
        }

        try
        {
            areaRepository.UpdateArea(area);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            return Error.Conflict("area_code_conflict", $"Area with code '{request.Code.Trim().ToUpperInvariant()}' already exists.");
        }

        return Result.Success();
    }
}
