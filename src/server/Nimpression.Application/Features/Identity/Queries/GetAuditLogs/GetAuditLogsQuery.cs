using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Identity.DTOs;

namespace Nimpression.Application.Features.Identity.Queries.GetAuditLogs;

public sealed record GetAuditLogsQuery(
    Guid? ActorUserId = null,
    string? EntityType = null,
    string? EntityId = null,
    string? Action = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<AuditEventDto>>>;
