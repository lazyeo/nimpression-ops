using FluentAssertions;
using Nimpression.Application.Features.Areas.DTOs;
using Nimpression.Application.Features.Areas.Queries.GetAreaAssignments;
using Nimpression.Application.Features.Areas.Queries.GetAreaById;
using Nimpression.Application.Features.Areas.Queries.GetAreasList;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Nimpression.Domain.Entities.Area;
using Xunit;

namespace Nimpression.Application.Tests.Areas.Queries;

public sealed class AreaQueriesHandlerTests
{
    private readonly FakeAreaRepository _repo = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();

    [Fact]
    public async Task GetAreasList_ReturnsPagedResults()
    {
        // Arrange
        var area1 = new Area(Guid.NewGuid(), "Auckland Central", "AKL-CBD");
        var area2 = new Area(Guid.NewGuid(), "North Shore", "AKL-NS");
        _repo.Areas[area1.Id] = area1;
        _repo.Areas[area2.Id] = area2;

        var handler = new GetAreasListQueryHandler(_repo);
        var query = new GetAreasListQuery(new AreaFilter(SearchTerm: "North"));

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(a => a.Code == "AKL-NS");
    }

    [Fact]
    public async Task GetAreaById_ExistingArea_ReturnsDetail()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "East Auckland", "AKL-EAST", "East side", null, true);
        _repo.Areas[area.Id] = area;

        var handler = new GetAreaByIdQueryHandler(_repo, _dateTimeProvider);
        var query = new GetAreaByIdQuery(area.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("East Auckland");
        result.Value.Code.Should().Be("AKL-EAST");
    }

    [Fact]
    public async Task GetAreaAssignments_ReturnsFilteredAssignments()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "South Auckland", "AKL-SOUTH");
        _repo.Areas[area.Id] = area;

        var driverId = Guid.NewGuid();
        var assignment = new AreaAssignment(
            Guid.NewGuid(),
            area.Id,
            driverId,
            new DateOnly(2026, 1, 1),
            null);
        _repo.Assignments[assignment.Id] = assignment;

        var handler = new GetAreaAssignmentsQueryHandler(_repo, _dateTimeProvider);
        var query = new GetAreaAssignmentsQuery(AreaId: area.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(a => a.AreaCode == "AKL-SOUTH" && a.DriverId == driverId);
    }
}
