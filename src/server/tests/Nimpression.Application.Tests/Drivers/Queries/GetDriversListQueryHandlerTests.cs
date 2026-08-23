using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Application.Features.Drivers.Queries.GetDriversList;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Queries;

public sealed class GetDriversListQueryHandlerTests
{
    private readonly IDriverRepository _driverRepository = Substitute.For<IDriverRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private GetDriversListQueryHandler CreateSut()
    {
        _dateTimeProvider.NzToday.Returns(new DateOnly(2026, 8, 24));
        return new GetDriversListQueryHandler(_driverRepository, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_queries_repository_with_filter_and_returns_paged_result()
    {
        var sut = CreateSut();
        var filter = new DriverFilter(SearchTerm: "Liam", Page: 1, PageSize: 20);

        var expectedSummary = new DriverSummaryDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "DRV-001",
            "Liam Smith",
            "liam.smith@nimpression.co.nz",
            "Class 4",
            new DateOnly(2027, 5, 20),
            false,
            false,
            270,
            DriverStatus.Active,
            new DateOnly(2024, 1, 15),
            32.50m,
            45.00m,
            0.85m,
            ["Auckland Central"],
            [Guid.NewGuid()],
            null);

        var pagedResult = new PagedResult<DriverSummaryDto>([expectedSummary], 1, 1, 20);

        _driverRepository.GetDriversPagedAsync(filter, new DateOnly(2026, 8, 24), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var query = new GetDriversListQuery(filter);
        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].DisplayName.Should().Be("Liam Smith");
    }
}
