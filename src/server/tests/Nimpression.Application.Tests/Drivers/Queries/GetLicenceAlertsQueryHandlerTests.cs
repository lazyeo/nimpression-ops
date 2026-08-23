using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Application.Features.Drivers.Queries.GetLicenceAlerts;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Queries;

public sealed class GetLicenceAlertsQueryHandlerTests
{
    private readonly IDriverRepository _driverRepository = Substitute.For<IDriverRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private GetLicenceAlertsQueryHandler CreateSut()
    {
        _dateTimeProvider.NzToday.Returns(new DateOnly(2026, 8, 24));
        return new GetLicenceAlertsQueryHandler(_driverRepository, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_returns_drivers_with_expiring_or_expired_licence()
    {
        var sut = CreateSut();
        var alert = new DriverLicenceAlertDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "DRV-004",
            "Jack Brown",
            "Class 2",
            new DateOnly(2026, 9, 13), // 20 days left
            20,
            false,
            DriverStatus.Active);

        _driverRepository.GetExpiringLicencesAsync(new DateOnly(2026, 8, 24), 30, Arg.Any<CancellationToken>())
            .Returns([alert]);

        var query = new GetLicenceAlertsQuery(30);
        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].EmployeeNo.Should().Be("DRV-004");
        result.Value[0].DaysUntilExpiry.Should().Be(20);
        result.Value[0].IsExpired.Should().BeFalse();
    }
}
