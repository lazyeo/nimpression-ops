using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.DTOs;

namespace Nimpression.Application.Features.Identity.Queries.GetAuditLogs;

public sealed class GetAuditLogsQueryHandler(IIdentityRepository identityRepository)
    : IRequestHandler<GetAuditLogsQuery, Result<PagedResult<AuditEventDto>>>
{
    public async Task<Result<PagedResult<AuditEventDto>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize is <= 0 or > 100 ? 20 : request.PageSize;

        var result = await identityRepository.QueryAuditLogsAsync(
            request.ActorUserId,
            request.EntityType,
            request.EntityId,
            request.Action,
            request.FromUtc,
            request.ToUtc,
            page,
            pageSize,
            cancellationToken);

        return result;
    }
}
