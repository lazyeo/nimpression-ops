using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Identity.DTOs;

namespace Nimpression.Application.Features.Identity.Queries.ExportAuditLogs;

public sealed record ExportAuditLogsQuery(
    Guid? ActorUserId = null,
    string? EntityType = null,
    string? EntityId = null,
    string? Action = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null) : IRequest<Result<AuditExportResult>>;
