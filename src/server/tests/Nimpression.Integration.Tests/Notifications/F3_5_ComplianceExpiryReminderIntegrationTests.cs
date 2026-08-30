using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.Common;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Notifications.Fixtures;

namespace Nimpression.Integration.Tests.Notifications;

[Collection("PostgreSqlCollection")]
public sealed class F3_5_ComplianceExpiryReminderIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly MailpitTestClient _mailpit = new();
    private TestDateTimeProvider _dateTimeProvider = null!;

    public F3_5_ComplianceExpiryReminderIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();

            if (!await db.EmailTemplates.AnyAsync(t => t.Key == NotificationTemplateKeys.ComplianceExpiryWarning))
            {
                var template = new EmailTemplate(
                    Guid.NewGuid(),
                    NotificationTemplateKeys.ComplianceExpiryWarning,
                    "Vehicle {{ExpiryType}} Expiry Warning - {{VehicleRego}}",
                    "车辆 {{ExpiryType}} 到期合规预警 - {{VehicleRego}}",
                    "Vehicle {{VehicleRego}} compliance item ({{ExpiryType}}) is expiring on {{ExpiryDate}}. Please book inspection.",
                    "车辆 {{VehicleRego}} 的 {{ExpiryType}} 即将于 {{ExpiryDate}} 到期，请及时预约年检与续保。",
                    true);

                await db.EmailTemplates.AddAsync(template);
                await db.SaveChangesAsync();
            }
        }

        _dateTimeProvider = TestDateTimeProvider.FromNzDate(2026, 9, 1);
        await _mailpit.ClearAllMessagesAsync();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _mailpit.Dispose();
    }

    [Fact]
    public async Task ComplianceExpiryScanner_Across30_14_7_DayBoundaries_GeneratesExactly3Logs_AndRerunProducesNoFourth()
    {
        // ── Step 1: 准备测试数据 ──
        // 车辆的 WOF 到期日设为 2026-10-01 (距离 2026-09-01 刚好 30 天)
        var wofExpiryDate = new DateOnly(2026, 10, 1);
        var rego = TestDataFactory.CreateRegoObject("EXP");
        var activePartnerEmail = TestDataFactory.CreateEmailAddress("active_vtnz");
        var inactivePartnerEmail = TestDataFactory.CreateEmailAddress("inactive_vtnz");

        using (var db = _fixture.CreateDbContext())
        {
            var vehicle = new Vehicle(
                Guid.NewGuid(),
                rego,
                "Toyota",
                "HiAce",
                2022,
                "VIN-COMPLIANCE-001",
                new Kilometres(45000m),
                new Kilometres(10000m),
                null,
                wofExpiryDate,
                null,
                null);

            var activePartner = new PartnerContact(
                Guid.NewGuid(),
                PartnerKind.Inspection,
                "VTNZ Albany (Active)",
                activePartnerEmail,
                true);

            var inactivePartner = new PartnerContact(
                Guid.NewGuid(),
                PartnerKind.Inspection,
                "VTNZ Closed Branch (Inactive)",
                inactivePartnerEmail,
                false);

            await db.Vehicles.AddAsync(vehicle);
            await db.PartnerContacts.AddAsync(activePartner);
            await db.PartnerContacts.AddAsync(inactivePartner);
            await db.SaveChangesAsync();
        }

        using var factory = new NotificationTestWebApplicationFactory(_fixture.ConnectionString, _dateTimeProvider);

        // ── Step 2: 边界 1 — 30 天前（2026-09-01，差值 30 天） ──
        _dateTimeProvider.SetNzToday(new DateOnly(2026, 9, 1));
        using (var scope1 = factory.Services.CreateScope())
        {
            var scanner = scope1.ServiceProvider.GetRequiredService<IComplianceExpiryScanner>();
            var sent = await scanner.ScanAndNotifyAsync();
            sent.IsSuccess.Should().BeTrue();
            sent.Value.Should().BeGreaterThanOrEqualTo(1, "到期前 30 天应触发预警邮件");
        }

        // ── Step 3: 边界 2 — 14 天前（2026-09-17，差值 14 天） ──
        _dateTimeProvider.SetNzToday(new DateOnly(2026, 9, 17));
        using (var scope2 = factory.Services.CreateScope())
        {
            var scanner = scope2.ServiceProvider.GetRequiredService<IComplianceExpiryScanner>();
            var sent = await scanner.ScanAndNotifyAsync();
            sent.IsSuccess.Should().BeTrue();
            sent.Value.Should().BeGreaterThanOrEqualTo(1, "到期前 14 天应触发预警邮件");
        }

        // ── Step 4: 边界 3 — 7 天前（2026-09-24，差值 7 天） ──
        _dateTimeProvider.SetNzToday(new DateOnly(2026, 9, 24));
        using (var scope3 = factory.Services.CreateScope())
        {
            var scanner = scope3.ServiceProvider.GetRequiredService<IComplianceExpiryScanner>();
            var sent = await scanner.ScanAndNotifyAsync();
            sent.IsSuccess.Should().BeTrue();
            sent.Value.Should().BeGreaterThanOrEqualTo(1, "到期前 7 天应触发预警邮件");
        }

        // ── Step 5: 重跑调度 — 2026-09-24 再次执行扫描（幂等去重测试） ──
        using (var scopeRerun = factory.Services.CreateScope())
        {
            var scanner = scopeRerun.ServiceProvider.GetRequiredService<IComplianceExpiryScanner>();
            var sent = await scanner.ScanAndNotifyAsync();
            sent.IsSuccess.Should().BeTrue();
        }

        // ── Step 6: 数据库最终断言 ──
        using (var verifyDb = _fixture.CreateDbContext())
        {
            // 断言活跃伙伴对本测试车辆恰好生成 3 条 Sent 记录（30/14/7 三个阈值，重跑不增加第 4 条）
            var activeLogs = await verifyDb.EmailLogs
                .Where(el => el.ToAddress == activePartnerEmail && el.Subject.Contains(rego.Value))
                .OrderBy(el => el.CorrelationId)
                .ToListAsync();

            activeLogs.Should().HaveCount(3, "30/14/7 天三个阈值各发一次，重跑不产生第 4 条，EmailLog 恰好 3 条");
            activeLogs.All(l => l.Status == "Sent").Should().BeTrue();

            activeLogs.Select(l => l.CorrelationId).Should().BeEquivalentTo([
                $"CORR-WOF-{rego.Value}-30DAY",
                $"CORR-WOF-{rego.Value}-14DAY",
                $"CORR-WOF-{rego.Value}-7DAY"
            ]);

            // 断言停用的联系人（Active=false）绝无任何邮件发送日志（F11.1）
            var inactiveLogs = await verifyDb.EmailLogs
                .Where(el => el.ToAddress == inactivePartnerEmail)
                .ToListAsync();

            inactiveLogs.Should().BeEmpty("停用联系人绝对不能接收到任何邮件");
        }
    }
}
