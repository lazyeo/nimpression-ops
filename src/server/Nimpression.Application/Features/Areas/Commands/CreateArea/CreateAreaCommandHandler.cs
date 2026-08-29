using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Application.Features.Areas.Common;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Application.Features.Areas.Commands.CreateArea;

/// <summary>
/// 创建运营区域命令处理器。
/// 关键并发保证：禁止先查后写（TOCTOU），通过数据库层唯一索引保证 Code 唯一性，
/// 捕获唯一约束异常并翻译为 409 Conflict。
/// </summary>
public sealed class CreateAreaCommandHandler(
    IAreaRepository areaRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateAreaCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAreaCommand request, CancellationToken cancellationToken)
    {
        Area area;
        try
        {
            area = new Area(
                Guid.NewGuid(),
                request.Name,
                request.Code,
                request.Description,
                request.GeoJson,
                request.IsActive);
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_area_data", ex.Message);
        }

        try
        {
            await areaRepository.AddAreaAsync(area, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            return Error.Conflict("area_code_conflict", $"Area with code '{request.Code.Trim().ToUpperInvariant()}' already exists.");
        }

        return area.Id;
    }
}
