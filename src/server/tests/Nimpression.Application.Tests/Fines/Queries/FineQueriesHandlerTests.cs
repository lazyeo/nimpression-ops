using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Application.Features.Fines.Abstractions;
using Nimpression.Application.Features.Fines.DTOs;
using Nimpression.Application.Features.Fines.Queries.GetFineById;
using Nimpression.Application.Features.Fines.Queries.GetFinePhotoUrl;
using Nimpression.Application.Features.Fines.Queries.GetFinesList;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Fines.Queries;

public sealed class FineQueriesHandlerTests
{
    private readonly IFineRepository _fineRepository = Substitute.For<IFineRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IObjectStorageService _storageService = Substitute.For<IObjectStorageService>();

    private readonly Guid _driverAUserId = Guid.NewGuid();
    private readonly Guid _driverADriverId = Guid.NewGuid();
    private readonly Guid _driverBDriverId = Guid.NewGuid();

    public FineQueriesHandlerTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(_driverAUserId);
        _fineRepository.GetDriverIdByUserIdAsync(_driverAUserId, Arg.Any<CancellationToken>())
            .Returns(_driverADriverId);
    }

    #region F8.4: 照片预签名 URL 与越权 403 防护

    /// <summary>
    /// F8.4 核心验收测试：司机 A 请求司机 B 的罚单照片预签名 URL，必须返回 403 Forbidden（非 404）。
    /// </summary>
    [Fact]
    public async Task F8_4_GetFinePhotoUrl_DriverA_Requests_DriverB_FinePhoto_Returns_403_Forbidden()
    {
        // Arrange
        var fineBId = Guid.NewGuid();
        var fineB = new Fine(
            fineBId,
            _driverBDriverId, // 属于司机 B
            Guid.NewGuid(),
            new DateOnly(2026, 8, 15),
            "NZ Police",
            "REF-B-001",
            new Money(150m),
            "Speeding",
            "fines/ticket_driver_b.jpg");

        _fineRepository.GetByIdAsync(fineBId, Arg.Any<CancellationToken>()).Returns(fineB);

        var handler = new GetFinePhotoUrlQueryHandler(_fineRepository, _currentUser, _storageService);
        var query = new GetFinePhotoUrlQuery(fineBId);

        // Act: 司机 A 发起请求
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert: 必须 403 Forbidden
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");

        await _storageService.DidNotReceive().GetPresignedUrlAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task F8_4_GetFinePhotoUrl_DriverA_Requests_Own_FinePhoto_Returns_PresignedUrl_With_15min_Expiry()
    {
        // Arrange
        var fineAId = Guid.NewGuid();
        const string photoKey = "fines/ticket_driver_a.jpg";
        const string expectedUrl = "https://s3.local/nimpression-media/fines/ticket_driver_a.jpg?signature=xyz";

        var fineA = new Fine(
            fineAId,
            _driverADriverId, // 属于司机 A
            Guid.NewGuid(),
            new DateOnly(2026, 8, 15),
            "NZ Police",
            "REF-A-001",
            new Money(150m),
            "Speeding",
            photoKey);

        _fineRepository.GetByIdAsync(fineAId, Arg.Any<CancellationToken>()).Returns(fineA);
        _storageService.GetPresignedUrlAsync(
            "nimpression-media",
            photoKey,
            Arg.Is<TimeSpan>(t => t <= TimeSpan.FromMinutes(15)),
            Arg.Any<CancellationToken>()).Returns(expectedUrl);

        var handler = new GetFinePhotoUrlQueryHandler(_fineRepository, _currentUser, _storageService);
        var query = new GetFinePhotoUrlQuery(fineAId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedUrl);
    }

    [Fact]
    public async Task GetFineById_DriverA_Requests_DriverB_FineDetail_Returns_403_Forbidden()
    {
        // Arrange
        var fineBId = Guid.NewGuid();
        var detailB = new FineDetailDto(
            fineBId,
            _driverBDriverId,
            "Driver Bob",
            "DRV-002",
            Guid.NewGuid(),
            "V12345",
            new DateOnly(2026, 8, 15),
            "NZ Police",
            "REF-B-001",
            150m,
            "NZD",
            "Speeding",
            FineStatus.Submitted,
            "fines/ticket_b.jpg",
            null,
            null,
            null,
            null,
            null);

        _fineRepository.GetFineDetailByIdAsync(fineBId, Arg.Any<CancellationToken>()).Returns(detailB);

        var handler = new GetFineByIdQueryHandler(_fineRepository, _currentUser, _storageService);
        var query = new GetFineByIdQuery(fineBId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task GetFinesList_DriverA_Specifying_DriverB_Id_Returns_403_Forbidden()
    {
        // Arrange
        var handler = new GetFinesListQueryHandler(_fineRepository, _currentUser);
        var filter = new FineFilter(DriverId: _driverBDriverId);
        var query = new GetFinesListQuery(filter);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    #endregion
}
