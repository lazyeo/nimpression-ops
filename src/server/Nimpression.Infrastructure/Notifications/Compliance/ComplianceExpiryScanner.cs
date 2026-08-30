using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.Common;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Notifications.Compliance;

/// <summary>
/// 车辆合规到期预警定时扫描器（F3.5 / F11）。
/// <para>
/// 核心特性：<br/>
/// 1. <b>确定性时钟注入</b>：严格使用 <see cref="IDateTimeProvider.NzToday"/>，禁止依赖真实时钟，确保 30/14/7 天边界可精确测试。<br/>
/// 2. <b>精确触发</b>：到期前 30/14/7 天各发一次，EmailLog 恰好 3 条。<br/>
/// 3. <b>幂等不重复</b>：基于 CorrelationId 唯一判定，同一到期日重跑调度绝不产生第 4 条。<br/>
/// 4. <b>伙伴状态过滤</b>：停用的联系人（Active=false）绝不发送邮件。
/// </para>
/// </summary>
public sealed partial class ComplianceExpiryScanner(
    AppDbContext dbContext,
    IEmailSender emailSender,
    IDateTimeProvider dateTimeProvider,
    ILogger<ComplianceExpiryScanner> logger) : IComplianceExpiryScanner
{
    private static readonly int[] AlertThresholdDays = [30, 14, 7];

    public async Task<Result<int>> ScanAndNotifyAsync(CancellationToken cancellationToken = default)
    {
        var today = dateTimeProvider.NzToday;
        LogScanStarting(logger, today);

        // 1. 查询所有有效车辆
        var vehicles = await dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.Status == VehicleStatus.Active)
            .ToListAsync(cancellationToken);

        // 2. 查询活跃的年检伙伴联系人（F11.1：停用联系人不发邮件）
        var activePartners = await dbContext.PartnerContacts
            .Where(pc => pc.Kind == PartnerKind.Inspection && pc.Active)
            .ToListAsync(cancellationToken);

        if (activePartners.Count == 0)
        {
            LogNoActiveInspectionPartners(logger);
            return 0;
        }

        // 3. 加载模板
        var template = await dbContext.EmailTemplates
            .FirstOrDefaultAsync(t => t.Key == NotificationTemplateKeys.ComplianceExpiryWarning, cancellationToken);

        var templateKey = template?.Key ?? NotificationTemplateKeys.ComplianceExpiryWarning;
        var subjectEnPattern = template?.SubjectEn ?? "Vehicle {{ExpiryType}} Expiry Warning - {{VehicleRego}}";
        var subjectZhPattern = template?.SubjectZh ?? "车辆 {{ExpiryType}} 到期合规预警 - {{VehicleRego}}";
        var bodyEnPattern = template?.BodyEn ?? "Vehicle {{VehicleRego}} compliance item ({{ExpiryType}}) is expiring on {{ExpiryDate}}. Please book inspection.";
        var bodyZhPattern = template?.BodyZh ?? "车辆 {{VehicleRego}} 的 {{ExpiryType}} 即将于 {{ExpiryDate}} 到期，请及时预约年检与续保。";

        var sentCount = 0;

        foreach (var vehicle in vehicles)
        {
            // 检查 WOF
            if (vehicle.WofExpiry.HasValue)
            {
                sentCount += await CheckAndSendExpiryAlertAsync(
                    vehicle, "WOF", vehicle.WofExpiry.Value, today, activePartners,
                    templateKey, subjectEnPattern, subjectZhPattern, bodyEnPattern, bodyZhPattern, cancellationToken);
            }

            // 检查 COF
            if (vehicle.CofExpiry.HasValue)
            {
                sentCount += await CheckAndSendExpiryAlertAsync(
                    vehicle, "COF", vehicle.CofExpiry.Value, today, activePartners,
                    templateKey, subjectEnPattern, subjectZhPattern, bodyEnPattern, bodyZhPattern, cancellationToken);
            }

            // 检查保险
            if (vehicle.InsuranceExpiry.HasValue)
            {
                sentCount += await CheckAndSendExpiryAlertAsync(
                    vehicle, "Insurance", vehicle.InsuranceExpiry.Value, today, activePartners,
                    templateKey, subjectEnPattern, subjectZhPattern, bodyEnPattern, bodyZhPattern, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        LogScanCompleted(logger, sentCount);
        return sentCount;
    }

    private async Task<int> CheckAndSendExpiryAlertAsync(
        Vehicle vehicle,
        string expiryType,
        DateOnly expiryDate,
        DateOnly today,
        List<PartnerContact> activePartners,
        string templateKey,
        string subjectEnPattern,
        string subjectZhPattern,
        string bodyEnPattern,
        string bodyZhPattern,
        CancellationToken cancellationToken)
    {
        var daysRemaining = expiryDate.DayNumber - today.DayNumber;
        if (!AlertThresholdDays.Contains(daysRemaining))
        {
            return 0;
        }

        var correlationId = $"CORR-{expiryType.ToUpperInvariant()}-{vehicle.Rego.Value}-{daysRemaining}DAY";
        var expiryDateStr = expiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var subjectEn = subjectEnPattern
            .Replace("{{ExpiryType}}", expiryType, StringComparison.OrdinalIgnoreCase)
            .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase)
            .Replace("{{ExpiryDate}}", expiryDateStr, StringComparison.OrdinalIgnoreCase);

        var subjectZh = subjectZhPattern
            .Replace("{{ExpiryType}}", expiryType, StringComparison.OrdinalIgnoreCase)
            .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase)
            .Replace("{{ExpiryDate}}", expiryDateStr, StringComparison.OrdinalIgnoreCase);

        var bodyEn = bodyEnPattern
            .Replace("{{ExpiryType}}", expiryType, StringComparison.OrdinalIgnoreCase)
            .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase)
            .Replace("{{ExpiryDate}}", expiryDateStr, StringComparison.OrdinalIgnoreCase);

        var bodyZh = bodyZhPattern
            .Replace("{{ExpiryType}}", expiryType, StringComparison.OrdinalIgnoreCase)
            .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase)
            .Replace("{{ExpiryDate}}", expiryDateStr, StringComparison.OrdinalIgnoreCase);

        var fullSubject = $"{subjectEn} / {subjectZh}";
        var fullBody = $"{bodyEn}\n\n{bodyZh}";

        var count = 0;

        foreach (var partner in activePartners)
        {
            // 幂等去重：检查该 CorrelationId + 邮箱是否已发过（F11.4 / F3.5）
            var existingLog = await dbContext.EmailLogs
                .FirstOrDefaultAsync(el =>
                    el.CorrelationId == correlationId &&
                    el.ToAddress == partner.Email,
                    cancellationToken);

            if (existingLog is not null && string.Equals(existingLog.Status, "Sent", StringComparison.OrdinalIgnoreCase))
            {
                LogAlreadySent(logger, correlationId, partner.Email.Value);
                continue;
            }

            var emailLog = existingLog ?? new EmailLog(
                Guid.NewGuid(),
                templateKey,
                partner.Email,
                fullSubject,
                "BackgroundService.ComplianceMonitor",
                correlationId);

            if (existingLog is null)
            {
                await dbContext.EmailLogs.AddAsync(emailLog, cancellationToken);
            }

            try
            {
                await emailSender.SendEmailAsync(partner.Email.Value, fullSubject, fullBody, cancellationToken);
                emailLog.RecordSuccess(dateTimeProvider.UtcNow);
                count++;
            }
            catch (Exception ex)
            {
                // 严禁静默吞掉异常（_COMMON.md）：记录失败并进入退避重试
                LogEmailFailed(logger, ex, vehicle.Rego.Value, expiryType, partner.Email.Value);
                emailLog.RecordFailure(ex.Message);
            }
        }

        return count;
    }

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Information,
        Message = "Starting compliance expiry scan for date: {Today}")]
    private static partial void LogScanStarting(ILogger logger, DateOnly today);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Warning,
        Message = "No active Inspection partner contacts found. Skipping email delivery.")]
    private static partial void LogNoActiveInspectionPartners(ILogger logger);

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Information,
        Message = "Compliance expiry scan completed. Dispatched {Count} emails.")]
    private static partial void LogScanCompleted(ILogger logger, int count);

    [LoggerMessage(
        EventId = 4104,
        Level = LogLevel.Debug,
        Message = "Compliance alert {CorrelationId} for {Email} already sent. Skipping.")]
    private static partial void LogAlreadySent(ILogger logger, string correlationId, string email);

    [LoggerMessage(
        EventId = 4105,
        Level = LogLevel.Error,
        Message = "Failed to send compliance expiry email for {Rego} ({Type}) to {Email}")]
    private static partial void LogEmailFailed(ILogger logger, Exception exception, string rego, string type, string email);
}
