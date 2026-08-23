using System.Text;
using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.DTOs;

namespace Nimpression.Application.Features.Identity.Queries.ExportAuditLogs;

public sealed class ExportAuditLogsQueryHandler(IIdentityRepository identityRepository)
    : IRequestHandler<ExportAuditLogsQuery, Result<AuditExportResult>>
{
    public async Task<Result<AuditExportResult>> Handle(ExportAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var logs = await identityRepository.QueryAllAuditLogsAsync(
            request.ActorUserId,
            request.EntityType,
            request.EntityId,
            request.Action,
            request.FromUtc,
            request.ToUtc,
            cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("Id,OccurredAt,ActorUserId,ActorRole,Action,EntityType,EntityId,IpAddress,UserAgent,BeforeJson,AfterJson");

        foreach (var log in logs)
        {
            builder.Append(EscapeCsv(log.Id.ToString())).Append(',')
                   .Append(EscapeCsv(log.OccurredAt.ToString("O"))).Append(',')
                   .Append(EscapeCsv(log.ActorUserId?.ToString() ?? string.Empty)).Append(',')
                   .Append(EscapeCsv(log.ActorRole?.ToString() ?? string.Empty)).Append(',')
                   .Append(EscapeCsv(log.Action)).Append(',')
                   .Append(EscapeCsv(log.EntityType)).Append(',')
                   .Append(EscapeCsv(log.EntityId)).Append(',')
                   .Append(EscapeCsv(log.IpAddress ?? string.Empty)).Append(',')
                   .Append(EscapeCsv(log.UserAgent ?? string.Empty)).Append(',')
                   .Append(EscapeCsv(log.BeforeJson ?? string.Empty)).Append(',')
                   .Append(EscapeCsv(log.AfterJson ?? string.Empty))
                   .AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var fileName = $"audit-logs-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";

        return new AuditExportResult(bytes, fileName, "text/csv; charset=utf-8");
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
