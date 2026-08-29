using MediatR;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Fines.Queries.GetFinePhotoUrl;

/// <summary>
/// 获取交通罚单照片短时效预签名 URL 查询（F8.4）。
/// 包含 IDOR 越权校验：司机尝试获取他人罚单照片必须返回 403 Forbidden。
/// </summary>
public sealed record GetFinePhotoUrlQuery(Guid FineId) : IRequest<Result<string>>;
