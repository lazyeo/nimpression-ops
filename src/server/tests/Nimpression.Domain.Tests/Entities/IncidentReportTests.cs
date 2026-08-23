using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Tests.Entities;

public sealed class IncidentReportTests
{
    private static readonly string[] SamplePhotoKeys = ["photo1.jpg", "photo2.jpg"];

    [Fact]
    public void IncidentReport_initializes_and_emits_IncidentReported_event()
    {
        var id = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var report = new IncidentReport(
            id,
            driverId,
            vehicleId,
            now,
            "123 Queen St, Auckland",
            IncidentSeverity.Moderate,
            "Minor collision with parked car",
            SamplePhotoKeys,
            "ENC_THIRD_PARTY_INFO");

        Assert.Equal(id, report.Id);
        Assert.Equal(driverId, report.DriverId);
        Assert.Equal(vehicleId, report.VehicleId);
        Assert.Equal("123 Queen St, Auckland", report.Location);
        Assert.Equal(IncidentSeverity.Moderate, report.Severity);
        Assert.Equal(2, report.PhotoKeys.Count);
        Assert.True(report.ShouldNotifyInsurer);

        var domainEvent = Assert.IsType<IncidentReported>(Assert.Single(report.DomainEvents));
        Assert.Equal(id, domainEvent.IncidentId);
        Assert.Equal(driverId, domainEvent.DriverId);
        Assert.Equal(vehicleId, domainEvent.VehicleId);
        Assert.Equal(IncidentSeverity.Moderate, domainEvent.Severity);
    }

    [Theory]
    [InlineData(IncidentSeverity.Minor, false)]
    [InlineData(IncidentSeverity.Moderate, true)]
    [InlineData(IncidentSeverity.Major, true)]
    [InlineData(IncidentSeverity.Critical, true)]
    public void IncidentReport_insurer_notification_rule(IncidentSeverity severity, bool expectedNotify)
    {
        var report = new IncidentReport(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Location",
            severity,
            "Description");

        Assert.Equal(expectedNotify, report.ShouldNotifyInsurer);

        var notifyTime = DateTimeOffset.UtcNow;
        report.MarkInsurerNotified(notifyTime);
        Assert.Equal(notifyTime, report.InsurerNotifiedAt);
    }

    [Fact]
    public void IncidentReport_guards_and_photo_management()
    {
        Assert.Throws<DomainValidationException>(() => new IncidentReport(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow, "Loc", IncidentSeverity.Minor, "Desc"));

        Assert.Throws<DomainValidationException>(() => new IncidentReport(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTimeOffset.UtcNow, "Loc", IncidentSeverity.Minor, "Desc"));

        Assert.Throws<DomainValidationException>(() => new IncidentReport(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "", IncidentSeverity.Minor, "Desc"));

        Assert.Throws<DomainValidationException>(() => new IncidentReport(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "Loc", IncidentSeverity.Minor, "   "));

        var report = new IncidentReport(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "Loc", IncidentSeverity.Minor, "Desc");

        report.AddPhotoKey("photo3.jpg");
        Assert.Single(report.PhotoKeys);
        Assert.Throws<DomainValidationException>(() => report.AddPhotoKey("  "));
    }
}
