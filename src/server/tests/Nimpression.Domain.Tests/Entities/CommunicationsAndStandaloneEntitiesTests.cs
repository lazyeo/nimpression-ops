using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Entities;

public sealed class CommunicationsAndStandaloneEntitiesTests
{
    [Fact]
    public void NewsPost_and_NewsReadReceipt_lifecycle()
    {
        var authorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var post = new NewsPost(
            Guid.NewGuid(),
            authorId,
            "Safety Update",
            "Please check tyres",
            "请检查轮胎",
            NewsAudience.Drivers,
            now);

        Assert.Equal("Safety Update", post.Title);
        Assert.Equal("Please check tyres", post.BodyEn);
        Assert.Equal("请检查轮胎", post.BodyZh);
        Assert.Equal(NewsAudience.Drivers, post.Audience);
        Assert.True(post.IsActive);
        Assert.False(post.Pinned);

        var newsEvent = Assert.IsType<NewsPublished>(Assert.Single(post.DomainEvents));
        Assert.Equal(post.Id, newsEvent.NewsPostId);
        Assert.Equal(NewsAudience.Drivers, newsEvent.Audience);

        post.Pin(true);
        Assert.True(post.Pinned);

        post.Archive();
        Assert.False(post.IsActive);

        post.Restore();
        Assert.True(post.IsActive);

        post.UpdateContent("New Title", "New En", "New Zh", NewsAudience.All);
        Assert.Equal("New Title", post.Title);

        var receipt = new NewsReadReceipt(Guid.NewGuid(), post.Id, Guid.NewGuid(), now.AddMinutes(5));
        Assert.Equal(post.Id, receipt.NewsPostId);

        Assert.Throws<DomainValidationException>(() => new NewsPost(
            Guid.NewGuid(), Guid.Empty, "Title", "En", "Zh", NewsAudience.All, now));
    }

    [Fact]
    public void PartnerContact_and_EmailTemplate_and_EmailLog()
    {
        var partner = new PartnerContact(
            Guid.NewGuid(),
            PartnerKind.Insurer,
            "AA Insurance",
            new EmailAddress("claims@aainsurance.co.nz"));

        Assert.True(partner.Active);
        partner.Deactivate();
        Assert.False(partner.Active);
        partner.Activate();
        Assert.True(partner.Active);

        var template = new EmailTemplate(
            Guid.NewGuid(),
            "SERVICE_DUE",
            "Vehicle Service Due",
            "车辆保养提醒",
            "Vehicle {{Rego}} is due for service",
            "车辆 {{Rego}} 需要进行保养");

        Assert.Equal("SERVICE_DUE", template.Key);
        Assert.True(template.Active);

        var log = new EmailLog(
            Guid.NewGuid(),
            "SERVICE_DUE",
            new EmailAddress("service@garage.co.nz"),
            "Service Due",
            "ServiceThresholdEvaluator",
            "CORR-001");

        Assert.Equal("Pending", log.Status);
        Assert.Equal(0, log.Attempts);

        log.RecordFailure("SMTP Connection Failed");
        Assert.Equal("Failed", log.Status);
        Assert.Equal(1, log.Attempts);
        Assert.Equal("SMTP Connection Failed", log.LastError);

        log.RecordSuccess(DateTimeOffset.UtcNow);
        Assert.Equal("Sent", log.Status);
        Assert.Equal(2, log.Attempts);
        Assert.NotNull(log.SentAt);
    }

    [Fact]
    public void Standalone_AuditEvent_DSR_and_OutboxMessage()
    {
        var audit = new AuditEvent(
            Guid.NewGuid(),
            "UpdateDriverRates",
            "Driver",
            "EMP001",
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            UserRole.Admin,
            "{\"rate\":25}",
            "{\"rate\":30}",
            "127.0.0.1",
            "Mozilla/5.0");

        Assert.Equal("UpdateDriverRates", audit.Action);
        Assert.Equal("Driver", audit.EntityType);

        var dsr = new DataSubjectRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DataSubjectRequestKind.Export,
            DateTimeOffset.UtcNow);

        Assert.Equal("Pending", dsr.Status);
        dsr.Complete("exports/driver-123.zip", DateTimeOffset.UtcNow);
        Assert.Equal("Completed", dsr.Status);
        Assert.Equal("exports/driver-123.zip", dsr.ExportKey);

        var dsr2 = new DataSubjectRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DataSubjectRequestKind.Deletion,
            DateTimeOffset.UtcNow);
        dsr2.Reject("Active employment contract", DateTimeOffset.UtcNow);
        Assert.Equal("Rejected", dsr2.Status);
        Assert.Equal("Active employment contract", dsr2.RejectionReason);

        var outbox = new OutboxMessage(
            Guid.NewGuid(),
            "DriverDeactivated",
            "{\"driverId\":\"123\"}",
            DateTimeOffset.UtcNow);

        Assert.Equal(0, outbox.Attempts);
        outbox.RecordAttempt("Timeout");
        Assert.Equal(1, outbox.Attempts);
        Assert.Equal("Timeout", outbox.Error);

        outbox.MarkProcessed(DateTimeOffset.UtcNow);
        Assert.NotNull(outbox.ProcessedAt);
        Assert.Null(outbox.Error);
    }
}
