using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Privacy;

/// <summary>
/// 隐私合规仓储实现（AC N2.1 - N2.7）。
/// 负责数据导出聚合、保留策略清理（含严格 dry-run 模式）、不可逆匿名化以及隐私同意审计记录。
/// </summary>
public sealed class PrivacyRepository(AppDbContext dbContext) : IPrivacyRepository
{
    private const string CurrentPrivacyPolicyTitle = "Nimpression Ops Privacy Statement & Data Processing Notice";
    private const string CurrentPrivacyPolicySummary = "This privacy statement explains how Nimpression Ops collects, uses, retains, and secures personal information in full compliance with the New Zealand Privacy Act 2020 and Information Privacy Principles (IPPs).";

    public async Task<DriverPersonalDataExportDto?> GetDriverPersonalDataAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var driver = await dbContext.Drivers.AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);

        var userExport = new UserExportDto(
            user.Id,
            user.Email.Value,
            user.DisplayName,
            user.Role.ToString(),
            user.Status.ToString(),
            user.Locale,
            user.CreatedAt,
            user.LastLoginAt);

        DriverProfileExportDto? driverExport = null;
        var shifts = new List<ShiftExportDto>();
        var tasks = new List<JobTaskExportDto>();
        var payslips = new List<PayslipExportDto>();
        var incidents = new List<IncidentExportDto>();
        var fines = new List<FineExportDto>();

        if (driver is not null)
        {
            driverExport = new DriverProfileExportDto(
                driver.Id,
                driver.EmployeeNo,
                driver.LicenceClass,
                driver.LicenceExpiry,
                driver.HiredOn,
                driver.Status.ToString(),
                driver.HourlyRate.Amount,
                driver.HourlyRate.Currency,
                driver.PerTripRate.Amount,
                driver.PerTripRate.Currency,
                driver.PerKmRate.Amount,
                driver.PerKmRate.Currency,
                driver.PhoneEnc,
                driver.AddressEnc,
                driver.EmergencyContactEnc);

            // 1. 班次打卡历史
            var dbShifts = await dbContext.ShiftEntries.AsNoTracking()
                .Where(s => s.DriverId == driver.Id)
                .OrderByDescending(s => s.ClockInAt)
                .ToListAsync(cancellationToken);

            shifts = dbShifts.Select(s => new ShiftExportDto(
                s.Id,
                s.ClockInAt,
                s.ClockOutAt,
                s.BreakMinutes,
                s.Status.ToString(),
                s.Note,
                s.CalculateWorkHours().Value)).ToList();

            // 2. 派单历史
            var dbTasks = await dbContext.JobTasks.AsNoTracking()
                .Where(t => t.DriverId == driver.Id)
                .OrderByDescending(t => t.ScheduledFor)
                .ToListAsync(cancellationToken);

            tasks = dbTasks.Select(t => new JobTaskExportDto(
                t.Id,
                t.Ref,
                t.Title,
                t.Description,
                t.ScheduledFor,
                t.Priority.ToString(),
                t.Status.ToString(),
                t.AcknowledgedAt,
                t.StartedAt,
                t.CompletedAt,
                t.PlannedDistanceKm?.Value,
                t.ActualDistanceKm?.Value)).ToList();

            // 3. 工资单与明细历史（包含 PayPeriod）
            var dbPayslips = await dbContext.Payslips.AsNoTracking()
                .Include(p => p.Lines)
                .Where(p => p.DriverId == driver.Id)
                .ToListAsync(cancellationToken);

            var periodIds = dbPayslips.Select(p => p.PayPeriodId).Distinct().ToList();
            var periods = await dbContext.PayPeriods.AsNoTracking()
                .Where(p => periodIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var inv = CultureInfo.InvariantCulture;
            payslips = dbPayslips.OrderByDescending(p => periods.TryGetValue(p.PayPeriodId, out var pp) ? pp.StartsOn : DateOnly.MinValue)
                .Select(p =>
                {
                    periods.TryGetValue(p.PayPeriodId, out var period);
                    var periodName = period != null
                        ? string.Format(inv, "Period {0:yyyy-MM-dd} - {1:yyyy-MM-dd}", period.StartsOn, period.EndsOn)
                        : "N/A";

                    return new PayslipExportDto(
                        p.Id,
                        p.PayPeriodId,
                        periodName,
                        period?.StartsOn ?? DateOnly.MinValue,
                        period?.EndsOn ?? DateOnly.MinValue,
                        p.BasisUsed.ToString(),
                        p.GrossPay.Amount,
                        p.GrossPay.Currency,
                        p.HoursBasedGross.Amount,
                        p.TripBasedGross.Amount,
                        p.MinimumWageTopUp,
                        p.CalculatedAt,
                        p.FinalisedAt,
                        p.Lines.Select(l => new PayslipLineExportDto(
                            l.Id,
                            l.Basis.ToString(),
                            l.Kind,
                            l.Description,
                            l.Hours?.Value,
                            l.Distance?.Value,
                            l.Qty,
                            l.Rate.Amount,
                            l.Amount.Amount)).ToList());
                }).ToList();

            // 4. 事故报告历史
            var dbIncidents = await dbContext.IncidentReports.AsNoTracking()
                .Where(i => i.DriverId == driver.Id)
                .OrderByDescending(i => i.OccurredAt)
                .ToListAsync(cancellationToken);

            incidents = dbIncidents.Select(i => new IncidentExportDto(
                i.Id,
                i.VehicleId,
                i.OccurredAt,
                i.Location,
                i.Severity.ToString(),
                i.Description,
                i.ThirdPartyInfoEnc,
                i.PhotoKeys)).ToList();

            // 5. 罚单历史
            var dbFines = await dbContext.Fines.AsNoTracking()
                .Where(f => f.DriverId == driver.Id)
                .OrderByDescending(f => f.IssuedOn)
                .ToListAsync(cancellationToken);

            fines = dbFines.Select(f => new FineExportDto(
                f.Id,
                f.Reference,
                f.IssuedOn,
                f.Amount.Amount,
                f.Status.ToString(),
                f.Reason)).ToList();
        }

        // 6. 隐私同意记录
        var consents = await dbContext.AuditEvents.AsNoTracking()
            .Where(a => a.ActorUserId == userId && (a.Action == "RecordPrivacyConsent" || a.Action == "Privacy.ConsentRecorded"))
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new ConsentRecordExportDto(
                ExtractPolicyVersion(a.AfterJson) ?? "2026.1",
                a.OccurredAt,
                a.IpAddress,
                a.UserAgent))
            .ToListAsync(cancellationToken);

        var metadata = new ExportMetadataDto(
            Guid.NewGuid(),
            userId,
            DateTimeOffset.UtcNow,
            "New Zealand Privacy Act 2020 Information Privacy Principle 6 (Access to personal information)",
            "Nimpression Ops Logistics NZ",
            "New Zealand (NZ)");

        return new DriverPersonalDataExportDto(
            metadata,
            userExport,
            driverExport,
            shifts,
            tasks,
            payslips,
            incidents,
            fines,
            consents);
    }

    public async Task<DataSubjectRequest?> GetLatestRequestAsync(
        Guid userId,
        DataSubjectRequestKind kind,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.DataSubjectRequests
            .Where(d => d.SubjectUserId == userId && d.Kind == kind)
            .OrderByDescending(d => d.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddDataSubjectRequestAsync(
        DataSubjectRequest request,
        CancellationToken cancellationToken = default)
    {
        await dbContext.DataSubjectRequests.AddAsync(request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RetentionCleanupReportDto> ExecuteRetentionCleanupAsync(
        DateTimeOffset referenceDate,
        bool execute,
        CancellationToken cancellationToken = default)
    {
        var summaries = new List<string>();
        var inv = CultureInfo.InvariantCulture;

        // 1. IPP 9: GPS 轨迹/打卡坐标 90 天清理策略
        var gpsCutoff = referenceDate.AddDays(-90);
        var gpsQuery = dbContext.ShiftEntries
            .Where(s => s.ClockInAt < gpsCutoff &&
                        (s.ClockInLat != null || s.ClockInLng != null || s.ClockOutLat != null || s.ClockOutLng != null));

        var gpsCount = await gpsQuery.CountAsync(cancellationToken);

        if (execute && gpsCount > 0)
        {
            await gpsQuery.ExecuteUpdateAsync(setter => setter
                .SetProperty(s => s.ClockInLat, (decimal?)null)
                .SetProperty(s => s.ClockInLng, (decimal?)null)
                .SetProperty(s => s.ClockOutLat, (decimal?)null)
                .SetProperty(s => s.ClockOutLng, (decimal?)null),
                cancellationToken);

            summaries.Add(string.Format(inv, "[LIVE] Purged GPS coordinates for {0} shift records older than 90 days ({1:yyyy-MM-dd}).", gpsCount, gpsCutoff));
        }
        else
        {
            summaries.Add(string.Format(inv, "[DRY-RUN] Found {0} shift records with GPS coordinates older than 90 days eligible for sanitization.", gpsCount));
        }

        // 2. 过期/已撤销 RefreshToken 清理策略 (30 天前)
        var tokenCutoff = referenceDate.AddDays(-30);
        var tokenQuery = dbContext.RefreshTokens
            .Where(r => r.ExpiresAt < referenceDate || (r.RevokedAt != null && r.RevokedAt < tokenCutoff));

        var tokenCount = await tokenQuery.CountAsync(cancellationToken);

        if (execute && tokenCount > 0)
        {
            await tokenQuery.ExecuteDeleteAsync(cancellationToken);
            summaries.Add(string.Format(inv, "[LIVE] Purged {0} expired/revoked refresh tokens.", tokenCount));
        }
        else
        {
            summaries.Add(string.Format(inv, "[DRY-RUN] Found {0} expired/revoked refresh tokens eligible for deletion.", tokenCount));
        }

        // 3. 过期邮件通信日志清理策略 (180 天前)
        var emailCutoff = referenceDate.AddDays(-180);
        var emailQuery = dbContext.EmailLogs
            .Where(e => e.SentAt != null && e.SentAt < emailCutoff);

        var emailCount = await emailQuery.CountAsync(cancellationToken);

        if (execute && emailCount > 0)
        {
            await emailQuery.ExecuteDeleteAsync(cancellationToken);
            summaries.Add(string.Format(inv, "[LIVE] Purged {0} transient email log entries older than 180 days ({1:yyyy-MM-dd}).", emailCount, emailCutoff));
        }
        else
        {
            summaries.Add(string.Format(inv, "[DRY-RUN] Found {0} transient email log entries older than 180 days eligible for deletion.", emailCount));
        }

        summaries.Add("[POLICY] AuditEvents and Payslip records are protected under mandatory 7-year statutory retention (Companies Act 1993 / Tax Administration Act 1994 s22) and excluded from automated purging.");

        return new RetentionCleanupReportDto(
            referenceDate,
            !execute,
            gpsCount,
            tokenCount,
            emailCount,
            referenceDate,
            summaries);
    }

    public async Task<AnonymizationResultDto> AnonymizeDriverAsync(
        Guid driverId,
        DateTimeOffset referenceDate,
        CancellationToken cancellationToken = default)
    {
        var driver = await dbContext.Drivers
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        if (driver is null)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Driver with ID '{0}' was not found for anonymization.", driverId));
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == driver.UserId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Associated User with ID '{0}' was not found.", driver.UserId));
        }

        // 匿名化前：计算并保存基准指标
        var grossPayBefore = await dbContext.Payslips
            .Where(p => p.DriverId == driverId)
            .SumAsync(p => p.GrossPay.Amount, cancellationToken);

        var payslipCountBefore = await dbContext.Payslips
            .CountAsync(p => p.DriverId == driverId, cancellationToken);

        var incidentCountBefore = await dbContext.IncidentReports
            .CountAsync(i => i.DriverId == driverId, cancellationToken);

        var auditCountBefore = await dbContext.AuditEvents
            .CountAsync(cancellationToken);

        // 执行不可逆脱敏替换（保留外键关系与所有历史数值记录）
        var shortGuid = driver.Id.ToString("N", CultureInfo.InvariantCulture)[..6];
        var anonDisplayName = string.Format(CultureInfo.InvariantCulture, "Driver #{0}", shortGuid);
        var anonEmail = new EmailAddress(string.Format(CultureInfo.InvariantCulture, "anon-{0}@privacy.internal", driver.Id.ToString("N", CultureInfo.InvariantCulture)[..8]));

        user.UpdateProfile(anonDisplayName, avatarKey: null, locale: "en-NZ");
        user.SetPasswordHash(string.Format(CultureInfo.InvariantCulture, "$ANON${0:N}${1:N}$", Guid.NewGuid(), Guid.NewGuid()));
        user.SetStatus(UserStatus.Inactive);

        // 采用 EF Core Entry 属性赋值更新 Email
        dbContext.Entry(user).Property(u => u.Email).CurrentValue = anonEmail;

        // 加密联系人字段替换为不可逆脱敏占位符
        driver.UpdateEncryptedContactInfo("ANONYMIZED", "ANONYMIZED", "ANONYMIZED");
        driver.SetStatus(DriverStatus.Inactive);

        // 记录或完成 DSR 删除/匿名化工单
        var dsr = new DataSubjectRequest(
            Guid.NewGuid(),
            user.Id,
            DataSubjectRequestKind.Deletion,
            referenceDate);
        dsr.Complete(null, referenceDate);
        await dbContext.DataSubjectRequests.AddAsync(dsr, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        // 匿名化后：再次聚合校验指标
        var grossPayAfter = await dbContext.Payslips
            .Where(p => p.DriverId == driverId)
            .SumAsync(p => p.GrossPay.Amount, cancellationToken);

        var payslipCountAfter = await dbContext.Payslips
            .CountAsync(p => p.DriverId == driverId, cancellationToken);

        var incidentCountAfter = await dbContext.IncidentReports
            .CountAsync(i => i.DriverId == driverId, cancellationToken);

        var auditCountAfter = await dbContext.AuditEvents
            .CountAsync(cancellationToken);

        return new AnonymizationResultDto(
            driverId,
            user.Id,
            referenceDate,
            anonDisplayName,
            grossPayBefore,
            grossPayAfter,
            payslipCountBefore,
            payslipCountAfter,
            incidentCountBefore,
            incidentCountAfter,
            auditCountBefore,
            auditCountAfter);
    }

    public async Task<PrivacyConsentDto> GetPrivacyConsentStatusAsync(
        Guid userId,
        string policyVersion,
        CancellationToken cancellationToken = default)
    {
        var latestConsent = await dbContext.AuditEvents.AsNoTracking()
            .Where(a => a.ActorUserId == userId &&
                        (a.Action == "RecordPrivacyConsent" || a.Action == "Privacy.ConsentRecorded") &&
                        a.AfterJson != null &&
                        a.AfterJson.Contains(policyVersion))
            .OrderByDescending(a => a.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        var hasConsented = latestConsent is not null;
        var consentedAt = latestConsent?.OccurredAt;
        var ipAddress = latestConsent?.IpAddress;

        var fullText = BuildFullPrivacyPolicyText(policyVersion);

        return new PrivacyConsentDto(
            userId,
            policyVersion,
            hasConsented,
            consentedAt,
            ipAddress,
            CurrentPrivacyPolicyTitle,
            CurrentPrivacyPolicySummary,
            fullText);
    }

    public async Task RecordPrivacyConsentAsync(
        Guid userId,
        string policyVersion,
        DateTimeOffset consentedAt,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            PolicyVersion = policyVersion,
            ConsentedAt = consentedAt,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Jurisdiction = "NZ (Privacy Act 2020)",
            NoticeAcknowledged = true
        });

        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            "RecordPrivacyConsent",
            "PrivacyConsent",
            userId.ToString(),
            consentedAt,
            userId,
            null,
            null,
            payload,
            ipAddress,
            userAgent);

        await dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DataSubjectRequestDto>> GetDataSubjectRequestsAsync(
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.DataSubjectRequests.AsNoTracking();

        if (userId.HasValue)
        {
            query = query.Where(d => d.SubjectUserId == userId.Value);
        }

        var list = await query
            .OrderByDescending(d => d.RequestedAt)
            .Select(d => new DataSubjectRequestDto(
                d.Id,
                d.SubjectUserId,
                d.Kind,
                d.Status,
                d.RequestedAt,
                d.CompletedAt,
                d.ExportKey,
                d.RejectionReason))
            .ToListAsync(cancellationToken);

        return list;
    }

    private static string? ExtractPolicyVersion(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("policyVersion", out var prop) ||
                doc.RootElement.TryGetProperty("PolicyVersion", out prop))
            {
                return prop.GetString();
            }
        }
        catch
        {
            // Ignored
        }
        return null;
    }

    private static string BuildFullPrivacyPolicyText(string version)
    {
        return $"""
        # Nimpression Ops Privacy Statement & Data Protection Notice
        **Version**: {version} | **Effective Date**: 2026-01-01 | **Jurisdiction**: New Zealand

        ## 1. Compliance with the New Zealand Privacy Act 2020
        Nimpression Ops is committed to protecting the privacy of our drivers, contractors, and staff in accordance with the 13 Information Privacy Principles (IPPs) set out in the Privacy Act 2020.

        ## 2. Information We Collect
        - **Identity & Contact Details**: Name, email, residential address, mobile phone number, emergency contacts (IPP 1-3).
        - **Operational & Fleet Data**: Driver licence status, vehicle assignments, shift start/end timestamps, task milestones.
        - **Geolocation Coordinates**: GPS coordinates recorded strictly at shift clock-in and clock-out to verify physical site attendance (retained for 90 days under IPP 9).
        - **Payroll & Financial Records**: Timesheet hours, trip records, gross earnings, PAYE tax, KiwiSaver contributions (retained for 7 years as required by the Tax Administration Act 1994 and Holidays Act 2003).

        ## 3. Data Protection & Encryption (IPP 5)
        All sensitive personal information (including phone numbers, physical addresses, emergency contacts, third-party accident disclosures, and vehicle VINs) is encrypted at rest using AES-256-GCM column-level encryption.

        ## 4. Cross-Border Data Flow & Sovereignty (IPP 12)
        Our primary cloud infrastructure is hosted in Australia East / AWS Sydney (ap-southeast-2), providing high-speed network connectivity (~25-30ms) to New Zealand while ensuring comparable data protection under the Australian Privacy Principles (APPs). All backups and field-level encryption keys are managed within New Zealand security perimeters.

        ## 5. Your Rights: Access (IPP 6) & Correction (IPP 7)
        You have the right to request a complete export of your personal data at any time via the self-service export function, or to request correction of inaccurate records by contacting the Privacy Officer at privacy@nimpression.co.nz.
        """;
    }
}
