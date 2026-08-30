using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.Common;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Notifications.Outbox;

/// <summary>
/// 通知发件箱（Outbox）消费与重试服务实现（F11.3, F11.4）。
/// <para>
/// 核心职责：<br/>
/// 1. <b>领域事件消费</b>：消费 ServiceThresholdReached、FineAccepted、IncidentReported 领域事件，并转化为邮件通知。<br/>
/// 2. <b>精准收件人与状态过滤</b>：按伙伴类型匹配，停用（Active=false）的伙伴绝不发信。<br/>
/// 3. <b>天然幂等去重（F11.4）</b>：基于唯一 CorrelationId 判定，重复触发只记录 1 条 Sent 日志。<br/>
/// 4. <b>阶梯退避重试（F11.3）</b>：失败邮件按 1/5/25 分钟阶梯退避重试至多 3 次。<br/>
/// 5. <b>绝不静默降级</b>：发送异常必须落库追踪，禁止空 catch。
/// </para>
/// </summary>
public sealed partial class NotificationOutboxService(
    AppDbContext dbContext,
    IEmailSender emailSender,
    IDateTimeProvider dateTimeProvider,
    ILogger<NotificationOutboxService> logger) : INotificationOutboxService
{
    private static readonly HashSet<string> RelevantEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ServiceThresholdReached",
        "FineAccepted",
        "IncidentReported"
    };

    public async Task<int> ProcessPendingOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        var outboxMessages = await dbContext.OutboxMessages
            .Where(m => RelevantEventTypes.Contains(m.Type))
            .OrderBy(m => m.OccurredAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var processedCount = 0;
        foreach (var msg in outboxMessages)
        {
            var handled = await ProcessSingleOutboxMessageCoreAsync(msg, cancellationToken);
            if (handled)
            {
                processedCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return processedCount;
    }

    public async Task<bool> ProcessOutboxMessageAsync(Guid outboxMessageId, CancellationToken cancellationToken = default)
    {
        var message = await dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == outboxMessageId, cancellationToken);

        if (message is null || !RelevantEventTypes.Contains(message.Type))
        {
            return false;
        }

        var handled = await ProcessSingleOutboxMessageCoreAsync(message, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return handled;
    }

    public async Task<int> ProcessRetryQueueAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        var failedLogs = await dbContext.EmailLogs
            .Where(el => el.Status == "Failed" && el.Attempts < 3)
            .OrderBy(el => el.SentAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var retryCount = 0;

        foreach (var log in failedLogs)
        {
            var backoff = log.Attempts switch
            {
                1 => TimeSpan.FromMinutes(1),
                2 => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromMinutes(25)
            };

            var lastAttemptTime = log.SentAt ?? now;
            if (now < lastAttemptTime + backoff)
            {
                continue;
            }

            LogRetryingEmail(logger, log.Id, log.Attempts + 1);

            var template = await dbContext.EmailTemplates
                .FirstOrDefaultAsync(t => t.Key == log.TemplateKey, cancellationToken);

            var body = template is not null
                ? $"{template.BodyEn}\n\n{template.BodyZh}"
                : log.Subject;

            try
            {
                await emailSender.SendEmailAsync(log.ToAddress.Value, log.Subject, body, cancellationToken);
                log.RecordSuccess(now);
                retryCount++;
            }
            catch (Exception ex)
            {
                LogRetryFailed(logger, ex, log.Attempts + 1, log.Id);
                log.RecordFailure(ex.Message);
            }
        }

        if (failedLogs.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return retryCount;
    }

    private async Task<bool> ProcessSingleOutboxMessageCoreAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(message.PayloadJson) ? "{}" : message.PayloadJson);
        var root = doc.RootElement;

        var typeNormalized = message.Type.Trim();
        if (typeNormalized.EndsWith("Event", StringComparison.OrdinalIgnoreCase))
        {
            typeNormalized = typeNormalized[..^5];
        }

        switch (typeNormalized)
        {
            case "ServiceThresholdReached":
                return await HandleServiceThresholdReachedAsync(root, cancellationToken);

            case "FineAccepted":
                return await HandleFineAcceptedAsync(root, cancellationToken);

            case "IncidentReported":
                return await HandleIncidentReportedAsync(root, cancellationToken);

            default:
                return false;
        }
    }

    private async Task<bool> HandleServiceThresholdReachedAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var vehicleId = TryGetGuid(root, "VehicleId", "vehicleId");
        var cycleNo = TryGetInt(root, "ServiceCycleNo", "serviceCycleNo") ?? 1;

        if (!vehicleId.HasValue)
        {
            return false;
        }

        var vehicle = await dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId.Value, cancellationToken);
        if (vehicle is null)
        {
            return false;
        }

        var correlationId = string.Format(CultureInfo.InvariantCulture, "CORR-SVC-{0}-CYCLE{1:D2}", vehicle.Rego.Value, cycleNo);

        var partners = await dbContext.PartnerContacts
            .Where(pc => pc.Kind == PartnerKind.Maintenance && pc.Active)
            .ToListAsync(cancellationToken);

        if (partners.Count == 0)
        {
            LogNoActiveMaintenancePartner(logger, correlationId);
            return false;
        }

        var template = await dbContext.EmailTemplates
            .FirstOrDefaultAsync(t => t.Key == NotificationTemplateKeys.ServiceDueReminder, cancellationToken);

        var templateKey = template?.Key ?? NotificationTemplateKeys.ServiceDueReminder;
        var subjectEn = (template?.SubjectEn ?? "Vehicle Service Threshold Notification - {{VehicleRego}}")
            .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase);
        var subjectZh = (template?.SubjectZh ?? "车辆保养里程阈值提醒 - {{VehicleRego}}")
            .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase);

        var odometerStr = vehicle.OdometerKm.Value.ToString("F0", CultureInfo.InvariantCulture);
        var bodyEn = (template?.BodyEn ?? "Vehicle {{VehicleRego}} has reached odometer {{CurrentOdometer}} km and is due for scheduled maintenance.")
            .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase)
            .Replace("{{CurrentOdometer}}", odometerStr, StringComparison.OrdinalIgnoreCase);
        var bodyZh = (template?.BodyZh ?? "车辆 {{VehicleRego}} 当前里程已达 {{CurrentOdometer}} 公里，已触发定期保养阈值，请尽快安排维护。")
            .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase)
            .Replace("{{CurrentOdometer}}", odometerStr, StringComparison.OrdinalIgnoreCase);

        var fullSubject = $"{subjectEn} / {subjectZh}";
        var fullBody = $"{bodyEn}\n\n{bodyZh}";

        return await SendToPartnersWithDeduplicationAsync(
            partners, templateKey, fullSubject, fullBody, "DomainEvent.ServiceThresholdReached", correlationId, cancellationToken);
    }

    private async Task<bool> HandleFineAcceptedAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var fineId = TryGetGuid(root, "FineId", "fineId");
        if (!fineId.HasValue)
        {
            return false;
        }

        var fine = await dbContext.Fines.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fineId.Value, cancellationToken);
        var fineRef = fine?.Reference ?? fineId.Value.ToString("N", CultureInfo.InvariantCulture)[..8].ToUpperInvariant();

        var correlationId = $"CORR-FINE-{fineRef}";

        var partners = await dbContext.PartnerContacts
            .Where(pc => pc.Kind == PartnerKind.Insurer && pc.Active)
            .ToListAsync(cancellationToken);

        if (partners.Count == 0)
        {
            LogNoActiveInsurerPartner(logger, correlationId);
            return false;
        }

        var template = await dbContext.EmailTemplates
            .FirstOrDefaultAsync(t => t.Key == NotificationTemplateKeys.FineAcceptedNotice, cancellationToken);

        var templateKey = template?.Key ?? NotificationTemplateKeys.FineAcceptedNotice;
        var subjectEn = (template?.SubjectEn ?? "Infringement Notice Accepted - Ref {{FineRef}}")
            .Replace("{{FineRef}}", fineRef, StringComparison.OrdinalIgnoreCase);
        var subjectZh = (template?.SubjectZh ?? "交通罚单责任确认通知 - 编号 {{FineRef}}")
            .Replace("{{FineRef}}", fineRef, StringComparison.OrdinalIgnoreCase);

        var bodyEn = (template?.BodyEn ?? "Infringement notice {{FineRef}} has been reviewed and accepted for payment processing.")
            .Replace("{{FineRef}}", fineRef, StringComparison.OrdinalIgnoreCase);
        var bodyZh = (template?.BodyZh ?? "交通罚单 {{FineRef}} 已由管理员审核确认，进入财务支付与对账流程。")
            .Replace("{{FineRef}}", fineRef, StringComparison.OrdinalIgnoreCase);

        var fullSubject = $"{subjectEn} / {subjectZh}";
        var fullBody = $"{bodyEn}\n\n{bodyZh}";

        return await SendToPartnersWithDeduplicationAsync(
            partners, templateKey, fullSubject, fullBody, "DomainEvent.FineAccepted", correlationId, cancellationToken);
    }

    private async Task<bool> HandleIncidentReportedAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var incidentId = TryGetGuid(root, "IncidentId", "incidentId");
        var vehicleId = TryGetGuid(root, "VehicleId", "vehicleId");
        var severityVal = TryGetInt(root, "Severity", "severity") ?? (int)IncidentSeverity.Minor;
        var severity = (IncidentSeverity)severityVal;

        if (severity < IncidentSeverity.Moderate)
        {
            LogIncidentSeverityBelowThreshold(logger, incidentId, severity);
            return false;
        }

        string regoStr = "UNKNOWN";
        if (vehicleId.HasValue)
        {
            var vehicle = await dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId.Value, cancellationToken);
            if (vehicle is not null)
            {
                regoStr = vehicle.Rego.Value;
            }
        }

        var correlationId = $"CORR-INC-{incidentId.GetValueOrDefault()}";

        var partners = await dbContext.PartnerContacts
            .Where(pc => pc.Kind == PartnerKind.Insurer && pc.Active)
            .ToListAsync(cancellationToken);

        if (partners.Count == 0)
        {
            LogNoActiveInsurerPartner(logger, correlationId);
            return false;
        }

        var template = await dbContext.EmailTemplates
            .FirstOrDefaultAsync(t => t.Key == NotificationTemplateKeys.IncidentNotification, cancellationToken);

        var templateKey = template?.Key ?? NotificationTemplateKeys.IncidentNotification;
        var subjectEn = (template?.SubjectEn ?? "Incident Notification - {{Severity}} - {{VehicleRego}}")
            .Replace("{{Severity}}", severity.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{{VehicleRego}}", regoStr, StringComparison.OrdinalIgnoreCase);
        var subjectZh = (template?.SubjectZh ?? "车队事故通报 - {{Severity}} - {{VehicleRego}}")
            .Replace("{{Severity}}", severity.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{{VehicleRego}}", regoStr, StringComparison.OrdinalIgnoreCase);

        var bodyEn = (template?.BodyEn ?? "An incident involving vehicle {{VehicleRego}} has been reported with severity level {{Severity}}.")
            .Replace("{{Severity}}", severity.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{{VehicleRego}}", regoStr, StringComparison.OrdinalIgnoreCase);
        var bodyZh = (template?.BodyZh ?? "车辆 {{VehicleRego}} 发生事故，严重度等级为 {{Severity}}，相关材料已归档。")
            .Replace("{{Severity}}", severity.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{{VehicleRego}}", regoStr, StringComparison.OrdinalIgnoreCase);

        var fullSubject = $"{subjectEn} / {subjectZh}";
        var fullBody = $"{bodyEn}\n\n{bodyZh}";

        return await SendToPartnersWithDeduplicationAsync(
            partners, templateKey, fullSubject, fullBody, "DomainEvent.IncidentReported", correlationId, cancellationToken);
    }

    private async Task<bool> SendToPartnersWithDeduplicationAsync(
        List<PartnerContact> partners,
        string templateKey,
        string subject,
        string body,
        string triggeredBy,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var anySent = false;
        var now = dateTimeProvider.UtcNow;

        foreach (var partner in partners)
        {
            var existingLog = await dbContext.EmailLogs
                .FirstOrDefaultAsync(el =>
                    el.CorrelationId == correlationId &&
                    el.ToAddress == partner.Email,
                    cancellationToken);

            if (existingLog is not null && string.Equals(existingLog.Status, "Sent", StringComparison.OrdinalIgnoreCase))
            {
                LogEmailAlreadySent(logger, correlationId, partner.Email.Value);
                continue;
            }

            var emailLog = existingLog ?? new EmailLog(
                Guid.NewGuid(),
                templateKey,
                partner.Email,
                subject,
                triggeredBy,
                correlationId);

            if (existingLog is null)
            {
                await dbContext.EmailLogs.AddAsync(emailLog, cancellationToken);
            }

            try
            {
                await emailSender.SendEmailAsync(partner.Email.Value, subject, body, cancellationToken);
                emailLog.RecordSuccess(now);
                anySent = true;
            }
            catch (Exception ex)
            {
                LogDeliveryFailed(logger, ex, correlationId, partner.Email.Value);
                emailLog.RecordFailure(ex.Message);
            }
        }

        return anySent;
    }

    private static Guid? TryGetGuid(JsonElement root, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (root.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String && Guid.TryParse(prop.GetString(), out var guid))
                {
                    return guid;
                }
            }
        }
        return null;
    }

    private static int? TryGetInt(JsonElement root, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (root.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val))
                {
                    return val;
                }
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsedInt))
                {
                    return parsedInt;
                }
            }
        }
        return null;
    }

    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Information,
        Message = "Retrying failed email delivery for EmailLog {Id} (Attempt {Attempt}/3)")]
    private static partial void LogRetryingEmail(ILogger logger, Guid id, int attempt);

    [LoggerMessage(
        EventId = 4202,
        Level = LogLevel.Error,
        Message = "Retry attempt {Attempt} failed for EmailLog {Id}")]
    private static partial void LogRetryFailed(ILogger logger, Exception exception, int attempt, Guid id);

    [LoggerMessage(
        EventId = 4203,
        Level = LogLevel.Warning,
        Message = "No active Maintenance partner found for {CorrelationId}")]
    private static partial void LogNoActiveMaintenancePartner(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = 4204,
        Level = LogLevel.Warning,
        Message = "No active Insurer partner found for {CorrelationId}")]
    private static partial void LogNoActiveInsurerPartner(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = 4205,
        Level = LogLevel.Information,
        Message = "Incident {IncidentId} severity {Severity} < Moderate. Skipping insurer notification.")]
    private static partial void LogIncidentSeverityBelowThreshold(ILogger logger, Guid? incidentId, IncidentSeverity severity);

    [LoggerMessage(
        EventId = 4206,
        Level = LogLevel.Debug,
        Message = "Email for {CorrelationId} to {Email} already marked Sent. Skipping.")]
    private static partial void LogEmailAlreadySent(ILogger logger, string correlationId, string email);

    [LoggerMessage(
        EventId = 4207,
        Level = LogLevel.Error,
        Message = "Failed delivery for {CorrelationId} to {Email}")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, string correlationId, string email);
}
