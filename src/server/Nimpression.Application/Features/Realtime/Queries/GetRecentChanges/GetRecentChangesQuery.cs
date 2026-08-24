using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Realtime.DTOs;

namespace Nimpression.Application.Features.Realtime.Queries.GetRecentChanges;

/// <summary>
/// 增量拉取指定时间点之后的失效变更信号列表（断线重连后补齐）。
/// </summary>
public sealed record GetRecentChangesQuery(
    DateTimeOffset Since,
    int Limit = 100) : IRequest<Result<List<RealtimeChangeDto>>>;
