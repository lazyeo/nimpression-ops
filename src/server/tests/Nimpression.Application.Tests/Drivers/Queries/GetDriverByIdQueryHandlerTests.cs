using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Application.Features.Drivers.Queries.GetDriverById;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Queries;

public sealed class GetDriverByIdQueryHandlerTests
{
    private readonly IDriverRepository _driverRepository = Substitute.For<IDriverRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IObjectStorageService _storageService = Substitute.For<IObjectStorageService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private GetDriverByIdQueryHandler CreateSut()
    {
        _dateTimeProvider.NzToday.Returns(new DateOnly(2026, 8, 24));
        return new GetDriverByIdQueryHandler(_driverRepository, _currentUser, _storageService, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_admin_can_view_any_driver_profile()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var detail = new DriverDetailDto(
            driverId,
            userId,
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
            "NZD",
            45.00m,
            "NZD",
            0.85m,
            "NZD",
            "+6421000001",
            "10 Queen St",
            "+6421999999",
            "en-NZ",
            "avatars/1.jpg",
            null,
            []);

        _driverRepository.GetDriverDetailByIdAsync(driverId, new DateOnly(2026, 8, 24), Arg.Any<CancellationToken>())
            .Returns(detail);
        _currentUser.Role.Returns(UserRole.Admin);
        _storageService.GetPresignedUrlAsync("nimpression-media", "avatars/1.jpg", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("http://minio:9000/signed-avatar");

        var query = new GetDriverByIdQuery(driverId);
        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Liam Smith");
        result.Value.AvatarUrl.Should().Be("http://minio:9000/signed-avatar");
    }

    [Fact]
    public async Task Handle_driver_viewing_other_drivers_profile_returns_forbidden_403()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var detail = new DriverDetailDto(
            driverId,
            driverUserId,
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
            "NZD",
            45.00m,
            "NZD",
            0.85m,
            "NZD",
            "phone",
            "addr",
            "emg",
            "en-NZ",
            null,
            null,
            []);

        _driverRepository.GetDriverDetailByIdAsync(driverId, new DateOnly(2026, 8, 24), Arg.Any<CancellationToken>())
            .Returns(detail);
        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(otherUserId);

        var query = new GetDriverByIdQuery(driverId);
        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");
    }

    [Fact]
    public async Task Handle_driver_not_found_returns_not_found_404()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();

        _driverRepository.GetDriverDetailByIdAsync(driverId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DriverDetailDto?)null);

        var query = new GetDriverByIdQuery(driverId);
        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("driver_not_found");
    }
}
