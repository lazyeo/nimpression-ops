using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.Common;
using Nimpression.Application.Features.Vehicles.Common;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Notifications.Compliance;

/// <summary>
/// 车辆合规到期预警扫描器实现（F3.5 &amp; F11.3 / F11.4）。
/// <para>
/// <b>核心规则：</b><br/>
/// 1. <b>基准时钟</b>：使用 <see cref="IDateTimeProvider.NzToday"/> 获取新西兰当前自然日。<br/>
/// 2. <b>合规项与阈值</b>：每日扫描 WOF / COF / 保险到期日，精准匹配 30 天、14 天、7 天窗口。<br/>
/// 3. <b>接收人过滤</b>：拉取活跃年检机构联系人（<see cref="PartnerKind.Inspection"/> 且 Active=true），停用联系人绝不发信。<br/>
/// 4. <b>原子去重与幂等（F11.4）</b>：先写占位并捕获数据库唯一索引约束（SqlState 23505），杜绝并发 TOCTOU，同一车同一阈值仅记录 1 条 Sent 日志与发信。<br/>
/// 5. <b>绝不静默降级</b>：发信异常记录于 EmailLog 追踪并重试。
/// </para>
/// </summary>
public sealed partial class ComplianceExpiryScanner(
    AppDbContext dbContext,
    IEmailSender emailSender,
    IDateTimeProvider dateTimeProvider,
    ILogger<ComplianceExpiryScanner> logger) : IComplianceExpiryScanner
{
    private static readonly int[] ThresholdDays = [30, 14, 7];

    public async Task<Result<int>> ScanAndNotifyAsync(CancellationToken cancellationToken = default)
    {
        var nzToday = dateTimeProvider.NzToday;
        var nowUtc = dateTimeProvider.UtcNow;

        var inspectionPartners = await dbContext.PartnerContacts
            .Where(pc => pc.Kind == PartnerKind.Inspection && pc.Active)
            .ToListAsync(cancellationToken);

        if (inspectionPartners.Count == 0)
        {
            LogNoActiveInspectionPartners(logger);
            return Result<int>.Success(0);
        }

        var template = await dbContext.EmailTemplates
            .FirstOrDefaultAsync(t => t.Key == NotificationTemplateKeys.ComplianceExpiryWarning, cancellationToken);

        var activeVehicles = await dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.Status == VehicleStatus.Active || v.Status == VehicleStatus.Maintenance)
            .ToListAsync(cancellationToken);

        var totalSentCount = 0;

        foreach (var vehicle in activeVehicles)
        {
            var expiryItems = new (string ExpiryType, DateOnly? ExpiryDate)[]
            {
                ("WOF", vehicle.WofExpiry),
                ("COF", vehicle.CofExpiry),
                ("Insurance", vehicle.InsuranceExpiry)
            };

            foreach (var (expiryType, expiryDate) in expiryItems)
            {
                if (!expiryDate.HasValue)
                {
                    continue;
                }

                var daysUntilExpiry = expiryDate.Value.DayNumber - nzToday.DayNumber;

                if (!ThresholdDays.Contains(daysUntilExpiry))
                {
                    continue;
                }

                var correlationId = string.Format(
                    CultureInfo.InvariantCulture,
                    "CORR-{0}-{1}-{2}DAY",
                    expiryType.ToUpperInvariant(),
                    vehicle.Rego.Value,
                    daysUntilExpiry);

                var templateKey = template?.Key ?? NotificationTemplateKeys.ComplianceExpiryWarning;
                var subjectEn = (template?.SubjectEn ?? "Vehicle {{ExpiryType}} Expiry Warning - {{VehicleRego}}")
                    .Replace("{{ExpiryType}}", expiryType, StringComparison.OrdinalIgnoreCase)
                    .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase);
                var subjectZh = (template?.SubjectZh ?? "车辆 {{ExpiryType}} 到期合规预警 - {{VehicleRego}}")
                    .Replace("{{ExpiryType}}", expiryType, StringComparison.OrdinalIgnoreCase)
                    .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase);

                var expiryDateStr = expiryDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var bodyEn = (template?.BodyEn ?? "Vehicle {{VehicleRego}} compliance item ({{ExpiryType}}) is expiring on {{ExpiryDate}}. Please book inspection.")
                    .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase)
                    .Replace("{{ExpiryType}}", expiryType, StringComparison.OrdinalIgnoreCase)
                    .Replace("{{ExpiryDate}}", expiryDateStr, StringComparison.OrdinalIgnoreCase);
                var bodyZh = (template?.BodyZh ?? "车辆 {{VehicleRego}} 的 {{ExpiryType}} 即将于 {{ExpiryDate}} 到期，请及时预约年检与续保。")
                    .Replace("{{VehicleRego}}", vehicle.Rego.Value, StringComparison.OrdinalIgnoreCase)
                    .Replace("{{ExpiryType}}", expiryType, StringComparison.OrdinalIgnoreCase)
                    .Replace("{{ExpiryDate}}", expiryDateStr, StringComparison.OrdinalIgnoreCase);

                var fullSubject = $"{subjectEn} / {subjectZh}";
                var fullBody = $"{bodyEn}\n\n{bodyZh}";

                foreach (var partner in inspectionPartners)
                {
                    var existingLog = await dbContext.EmailLogs
                        .FirstOrDefaultAsync(el =>
                            el.CorrelationId == correlationId &&
                            el.ToAddress == partner.Email,
                            cancellationToken);

                    if (existingLog is not null && string.Equals(existingLog.Status, "Sent", StringComparison.OrdinalIgnoreCase))
                    {
                        LogExpiryNoticeAlreadySent(logger, correlationId, partner.Email.Value);
                        continue;
                    }

                    EmailLog emailLog;
                    if (existingLog is null)
                    {
                        var newLog = new EmailLog(
                            Guid.NewGuid(),
                            templateKey,
                            partner.Email,
                            fullSubject,
                            "Scheduler.ComplianceExpiryScanner",
                            correlationId);

                        try
                        {
                            await dbContext.EmailLogs.AddAsync(newLog, cancellationToken);
                            await dbContext.SaveChangesAsync(cancellationToken);
                            emailLog = newLog;
                        }
                        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
                        {
                            dbContext.Entry(newLog).State = EntityState.Detached;

                            var winnerLog = await dbContext.EmailLogs
                                .FirstOrDefaultAsync(el =>
                                    el.CorrelationId == correlationId &&
                                    el.ToAddress == partner.Email,
                                    cancellationToken);

                            if (winnerLog is not null && (string.Equals(winnerLog.Status, "Sent", StringComparison.OrdinalIgnoreCase) || winnerLog.Attempts > 0))
                            {
                                LogExpiryNoticeAlreadySent(logger, correlationId, partner.Email.Value);
                                continue;
                            }

                            emailLog = winnerLog ?? newLog;
                        }
                    }
                    else
                    {
                        emailLog = existingLog;
                    }

                    try
                    {
                        await emailSender.SendEmailAsync(partner.Email.Value, fullSubject, fullBody, cancellationToken);
                        emailLog.RecordSuccess(nowUtc);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        totalSentCount++;
                    }
                    catch (Exception ex)
                    {
                        LogExpirySendFailed(logger, ex, correlationId, partner.Email.Value);
                        emailLog.RecordFailure(ex.Message);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }

        return Result<int>.Success(totalSentCount);
    }

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "No active Inspection partners configured in database.")]
    private static partial void LogNoActiveInspectionPartners(ILogger logger);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Debug,
        Message = "Compliance expiry email for {CorrelationId} to {Email} already sent. Skipping.")]
    private static partial void LogExpiryNoticeAlreadySent(ILogger logger, string correlationId, string email);

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Error,
        Message = "Failed delivering compliance expiry notice for {CorrelationId} to {Email}")]
    private static partial void LogExpirySendFailed(ILogger logger, Exception exception, string correlationId, string email);
}
