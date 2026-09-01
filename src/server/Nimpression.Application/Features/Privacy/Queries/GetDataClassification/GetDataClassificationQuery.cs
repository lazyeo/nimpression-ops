using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.DTOs;

namespace Nimpression.Application.Features.Privacy.Queries.GetDataClassification;

/// <summary>
/// 查询系统数据资产分级分类清单（AC N2.2）。
/// </summary>
public sealed record GetDataClassificationQuery : IRequest<Result<IReadOnlyList<DataClassificationDto>>>;

public sealed class GetDataClassificationQueryHandler : IRequestHandler<GetDataClassificationQuery, Result<IReadOnlyList<DataClassificationDto>>>
{
    public Task<Result<IReadOnlyList<DataClassificationDto>>> Handle(
        GetDataClassificationQuery request,
        CancellationToken cancellationToken)
    {
        var items = DataClassificationCatalog.GetAll();
        return Task.FromResult(Result<IReadOnlyList<DataClassificationDto>>.Success(items));
    }
}
