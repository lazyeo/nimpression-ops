using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Incidents.Abstractions;
using Nimpression.Application.Features.Incidents.Commands.ReportIncident;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Incidents.Commands;

public sealed class ReportIncidentCommandHandlerTests
{
    private readonly IIncidentRepository _incidentRepository = Substitute.For<IIncidentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly Guid _driverUserId = Guid.NewGuid();
    private readonly Guid _driverId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedNow = new(2026, 8, 22, 10, 0, 0, TimeSpan.FromHours(12));

    public ReportIncidentCommandHandlerTests()
    {
        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(_driverUserId);
        _incidentRepository.GetDriverIdByUserIdAsync(_driverUserId, Arg.Any<CancellationToken>())
            .Returns(_driverId);
        _incidentRepository.VehicleExistsAsync(_vehicleId, Arg.Any<CancellationToken>())
            .Returns(true);
        _dateTimeProvider.UtcNow.Returns(_fixedNow);
    }

    private ReportIncidentCommandHandler CreateSut()
    {
        return new ReportIncidentCommandHandler(_incidentRepository, _unitOfWork, _currentUser, _dateTimeProvider);
    }

    [Theory]
    [InlineData(IncidentSeverity.Moderate)]
    [InlineData(IncidentSeverity.Major)]
    [InlineData(IncidentSeverity.Critical)]
    public async Task Handle_severity_moderate_or_higher_sets_InsurerNotifiedAt_and_keeps_domain_event(IncidentSeverity severity)
    {
        // Arrange (F9.2: 严重度 ≥ Moderate 自动发领域事件通知保险方并记 InsurerNotifiedAt)
        var sut = CreateSut();
        var command = new ReportIncidentCommand(
            DriverId: null,
            VehicleId: _vehicleId,
            OccurredAt: _fixedNow.AddHours(-1),
            Location: "State Highway 1, Auckland",
            Severity: severity,
            Description: "Rear-end collision during wet weather",
            PhotoKeys: new List<string> { "incidents/photo1.jpg", "incidents/photo2.jpg" },
            ThirdPartyInfo: "Rego: ABC123, Name: John Smith, Phone: 021999888");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        await _incidentRepository.Received(1).AddAsync(
            Arg.Is<IncidentReport>(i =>
                i.DriverId == _driverId &&
                i.VehicleId == _vehicleId &&
                i.Severity == severity &&
                i.InsurerNotifiedAt == _fixedNow &&
                i.PhotoKeys.Count == 2 &&
                i.DomainEvents.Any(e => e is IncidentReported)),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_severity_minor_does_NOT_set_InsurerNotifiedAt_and_does_NOT_emit_event()
    {
        // Arrange (F9.2: Minor 不自动发领域事件且不记 InsurerNotifiedAt)
        var sut = CreateSut();
        var command = new ReportIncidentCommand(
            DriverId: null,
            VehicleId: _vehicleId,
            OccurredAt: _fixedNow.AddHours(-1),
            Location: "Depot loading bay",
            Severity: IncidentSeverity.Minor,
            Description: "Minor scrape on tailgate guard",
            PhotoKeys: new List<string> { "incidents/minor1.jpg" });

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _incidentRepository.Received(1).AddAsync(
            Arg.Is<IncidentReport>(i =>
                i.DriverId == _driverId &&
                i.Severity == IncidentSeverity.Minor &&
                i.InsurerNotifiedAt == null &&
                i.DomainEvents.Count == 0),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_admin_reports_incident_for_driver_success()
    {
        // Arrange (F9.1: 管理员可代为提交)
        _currentUser.Role.Returns(UserRole.Admin);
        _incidentRepository.DriverExistsAsync(_driverId, Arg.Any<CancellationToken>()).Returns(true);

        var sut = CreateSut();
        var command = new ReportIncidentCommand(
            DriverId: _driverId,
            VehicleId: _vehicleId,
            OccurredAt: _fixedNow.AddHours(-2),
            Location: "Albany Highway",
            Severity: IncidentSeverity.Moderate,
            Description: "Tree branch mirror impact",
            PhotoKeys: null);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _incidentRepository.Received(1).AddAsync(
            Arg.Is<IncidentReport>(i => i.DriverId == _driverId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_driver_submitting_for_other_driver_returns_403_forbidden()
    {
        // Arrange: 越权防护
        var otherDriverId = Guid.NewGuid();
        var sut = CreateSut();
        var command = new ReportIncidentCommand(
            DriverId: otherDriverId,
            VehicleId: _vehicleId,
            OccurredAt: _fixedNow.AddHours(-1),
            Location: "Queen St",
            Severity: IncidentSeverity.Minor,
            Description: "Scrape");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }
}
