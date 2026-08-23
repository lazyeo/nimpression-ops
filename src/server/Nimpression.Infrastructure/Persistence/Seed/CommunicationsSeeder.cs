using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class CommunicationsSeeder
{
    public static (
        List<PartnerContact> Partners,
        List<EmailTemplate> Templates,
        List<NewsPost> NewsPosts,
        List<NewsReadReceipt> ReadReceipts,
        List<EmailLog> EmailLogs) Generate(List<User> users)
    {
        var adminUser = users.First(u => u.Role == UserRole.Admin);
        var driverUsers = users.Where(u => u.Role == UserRole.Driver).ToList();

        // 1. 外部伙伴联系人 (3类伙伴)
        var partners = new List<PartnerContact>
        {
            new(
                new Guid("D0000000-0000-0000-0000-000000000001"),
                PartnerKind.Insurer,
                "Vero Commercial Fleet Insurance NZ",
                new EmailAddress("claims.fleet@vero.co.nz"),
                true),
            new(
                new Guid("D0000000-0000-0000-0000-000000000002"),
                PartnerKind.Maintenance,
                "Auckland Heavy Truck & Trailer Services",
                new EmailAddress("service.desk@akltruckrepairs.co.nz"),
                true),
            new(
                new Guid("D0000000-0000-0000-0000-000000000003"),
                PartnerKind.Inspection,
                "Vehicle Testing New Zealand (VTNZ Commercial)",
                new EmailAddress("commercial.cert@vtnz.co.nz"),
                true)
        };

        // 2. 邮件模板
        var templates = new List<EmailTemplate>
        {
            new(
                new Guid("E0000000-0000-0000-0000-000000000001"),
                "SERVICE_DUE_REMINDER",
                "Vehicle Service Threshold Notification - {{VehicleRego}}",
                "车辆保养里程阈值提醒 - {{VehicleRego}}",
                "Vehicle {{VehicleRego}} has reached odometer {{CurrentOdometer}} km and is due for scheduled maintenance.",
                "车辆 {{VehicleRego}} 当前里程已达 {{CurrentOdometer}} 公里，已触发定期保养阈值，请尽快安排维护。",
                true),
            new(
                new Guid("E0000000-0000-0000-0000-000000000002"),
                "COMPLIANCE_EXPIRY_WARNING",
                "Vehicle {{ExpiryType}} Expiry Warning - {{VehicleRego}}",
                "车辆 {{ExpiryType}} 到期合规预警 - {{VehicleRego}}",
                "Vehicle {{VehicleRego}} compliance item ({{ExpiryType}}) is expiring on {{ExpiryDate}}. Please book inspection.",
                "车辆 {{VehicleRego}} 的 {{ExpiryType}} 即将于 {{ExpiryDate}} 到期，请及时预约年检与续保。",
                true),
            new(
                new Guid("E0000000-0000-0000-0000-000000000003"),
                "INCIDENT_NOTIFICATION",
                "Incident Notification - {{Severity}} - {{VehicleRego}}",
                "车队事故通报 - {{Severity}} - {{VehicleRego}}",
                "An incident involving vehicle {{VehicleRego}} has been reported with severity level {{Severity}}.",
                "车辆 {{VehicleRego}} 发生事故，严重度等级为 {{Severity}}，相关材料已归档。",
                true),
            new(
                new Guid("E0000000-0000-0000-0000-000000000004"),
                "FINE_ACCEPTED_NOTICE",
                "Infringement Notice Accepted - Ref {{FineRef}}",
                "交通罚单责任确认通知 - 编号 {{FineRef}}",
                "Infringement notice {{FineRef}} has been reviewed and accepted for payment processing.",
                "交通罚单 {{FineRef}} 已由管理员审核确认，进入财务支付与对账流程。",
                true)
        };

        // 3. 新闻公告 (中英双语 + 司机受众与全员受众)
        var newsPosts = new List<NewsPost>
        {
            new(
                new Guid("F0000000-0000-0000-0000-000000000001"),
                adminUser.Id,
                "Auckland Transport Peak Clearway Enforcement Update",
                "Please note Auckland Transport is strictly enforcing commercial clearway zones between 07:00-09:00 and 16:30-18:30.",
                "请各位司机注意：奥克兰交通局（AT）自本月起对早晚高峰期公共快速车道与禁停路段加强执法，违停罚单将直接计入合规档案。",
                NewsAudience.All,
                SeedConstants.ReferenceNow.AddDays(-60),
                true,
                true),
            new(
                new Guid("F0000000-0000-0000-0000-000000000002"),
                adminUser.Id,
                "Winter Heavy Fleet Tyre Pressure & Pre-trip Check Protocol",
                "All drivers must complete daily pre-trip tyre pressure and tread depth checks during winter wet weather conditions.",
                "冬季雨天路滑，所有卡车司机出车前必须严格核查轮胎胎压与花纹深度，并将里程与仪表照片上传系统。",
                NewsAudience.Drivers,
                SeedConstants.ReferenceNow.AddDays(-30),
                false,
                true),
            new(
                new Guid("F0000000-0000-0000-0000-000000000003"),
                adminUser.Id,
                "New Bi-Weekly Hybrid Payroll Schedule Finalisation Policy",
                "Bi-weekly payroll calculations will be finalised every alternate Monday at 18:00 NZST.",
                "双周薪资计算与对账将于每隔周周一 18:00 定版，司机可在 App 查阅工时与趟次双口径明细。",
                NewsAudience.Drivers,
                SeedConstants.ReferenceNow.AddDays(-10),
                false,
                true)
        };

        // 4. 已读回执 (7/10 司机已读第 1 篇，5/10 司机已读第 2 篇)
        var readReceipts = new List<NewsReadReceipt>();
        var receiptCounter = 1;

        for (var i = 0; i < Math.Min(7, driverUsers.Count); i++)
        {
            readReceipts.Add(new NewsReadReceipt(
                new Guid($"11000000-0000-0000-0000-{receiptCounter++:D12}"),
                newsPosts[0].Id,
                driverUsers[i].Id,
                newsPosts[0].PublishedAt.AddHours(2 + i)));
        }

        for (var i = 0; i < Math.Min(5, driverUsers.Count); i++)
        {
            readReceipts.Add(new NewsReadReceipt(
                new Guid($"11000000-0000-0000-0000-{receiptCounter++:D12}"),
                newsPosts[1].Id,
                driverUsers[i].Id,
                newsPosts[1].PublishedAt.AddHours(1 + i)));
        }

        // 5. 邮件发送日志
        var emailLogs = new List<EmailLog>
        {
            new(
                new Guid($"12000000-0000-0000-0000-{1:D12}"),
                "SERVICE_DUE_REMINDER",
                partners[1].Email,
                "Vehicle Service Threshold Notification - NIM003",
                "BackgroundService.ThresholdEvaluator",
                "CORR-SVC-NIM003-CYCLE01"),
            new(
                new Guid($"12000000-0000-0000-0000-{2:D12}"),
                "COMPLIANCE_EXPIRY_WARNING",
                partners[2].Email,
                "Vehicle COF Expiry Warning - NIM005",
                "BackgroundService.ComplianceMonitor",
                "CORR-COF-NIM005-30DAY"),
            new(
                new Guid($"12000000-0000-0000-0000-{3:D12}"),
                "INCIDENT_NOTIFICATION",
                partners[0].Email,
                "Incident Notification - Major - NIM003",
                "DomainEvent.IncidentReported",
                "CORR-INC-202607-001")
        };

        emailLogs[0].RecordSuccess(SeedConstants.ReferenceNow.AddDays(-20));
        emailLogs[1].RecordSuccess(SeedConstants.ReferenceNow.AddDays(-10));
        emailLogs[2].RecordSuccess(SeedConstants.ReferenceNow.AddDays(-30));

        return (partners, templates, newsPosts, readReceipts, emailLogs);
    }
}
