using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Application.Features.Incidents.Abstractions;
using Nimpression.Application.Features.Incidents.DTOs;
using Nimpression.Application.Features.Incidents.Queries.GetIncidentById;
using Nimpression.Application.Features.Incidents.Queries.GetIncidentsList;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Incidents.Queries;

public sealed class IncidentQueriesHandlerTests
{
    private readonly IIncidentRepository _incidentRepository = Substitute.For<IIncidentRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IObjectStorageService _storageService = Substitute.For<IObjectStorageService>();

    private readonly Guid _driverAUserId = Guid.NewGuid();
    private readonly Guid _driverADriverId = Guid.NewGuid();
    private readonly Guid _driverBDriverId = Guid.NewGuid();

    public IncidentQueriesHandlerTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(_driverAUserId);
        _incidentRepository.GetDriverIdByUserIdAsync(_driverAUserId, Arg.Any<CancellationToken>())
            .Returns(_driverADriverId);
    }

    [Fact]
    public async Task GetIncidentById_DriverA_Requests_Own_Incident_Returns_Detail_With_Photo_Presigned_Urls()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        var detail = new IncidentReportDetailDto(
            incidentId,
            _driverADriverId,
            "Driver Dave",
            "DRV-001",
            Guid.NewGuid(),
            "REG-001",
            DateTimeOffset.UtcNow.AddDays(-5),
            "Penrose, Auckland",
            IncidentSeverity.Moderate,
            "Rear-end scrape",
            "Rego: XYZ999, Name: Bob",
            "Reported",
            DateTimeOffset.UtcNow.AddDays(-5),
            new List<string> { "incidents/photo1.jpg", "incidents/photo2.jpg" },
            new List<string>(),
            true);

        _incidentRepository.GetIncidentDetailByIdAsync(incidentId, Arg.Any<CancellationToken>()).Returns(detail);
        _storageService.GetPresignedUrlAsync(
            "nimpression-media",
            Arg.Any<string>(),
            Arg.Is<TimeSpan>(t => t <= TimeSpan.FromMinutes(15)),
            Arg.Any<CancellationToken>()).Returns("https://s3.local/presigned_url");

        var handler = new GetIncidentByIdQueryHandler(_incidentRepository, _currentUser, _storageService);
        var query = new GetIncidentByIdQuery(incidentId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PhotoUrls.Should().HaveCount(2);
        result.Value.ThirdPartyInfo.Should().Be("Rego: XYZ999, Name: Bob");
    }

    [Fact]
    public async Task GetIncidentById_DriverA_Requests_DriverB_Incident_Returns_403_Forbidden()
    {
        // Arrange (IDOR 越权拦截)
        var incidentId = Guid.NewGuid();
        var detail = new IncidentReportDetailDto(
            incidentId,
            _driverBDriverId, // 属于司机 B
            "Driver Bob",
            "DRV-002",
            Guid.NewGuid(),
            "REG-002",
            DateTimeOffset.UtcNow.AddDays(-2),
            "Auckland CBD",
            IncidentSeverity.Minor,
            "Minor scrape",
            null,
            "Reported",
            null,
            new List<string>(),
            new List<string>(),
            false);

        _incidentRepository.GetIncidentDetailByIdAsync(incidentId, Arg.Any<CancellationToken>()).Returns(detail);

        var handler = new GetIncidentByIdQueryHandler(_incidentRepository, _currentUser, _storageService);
        var query = new GetIncidentByIdQuery(incidentId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task GetIncidentsList_DriverA_Specifying_DriverB_Id_Returns_403_Forbidden()
    {
        // Arrange
        var handler = new GetIncidentsListQueryHandler(_incidentRepository, _currentUser);
        var filter = new IncidentFilter(DriverId: _driverBDriverId);
        var query = new GetIncidentsListQuery(filter);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }
}
