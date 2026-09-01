using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Application.Features.Privacy.Queries.ExportPersonalData;
using Nimpression.Application.Tests.Privacy.TestDoubles;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Privacy.Queries;

public sealed class ExportPersonalDataQueryHandlerTests
{
    private readonly FakePrivacyRepository _privacyRepository = new();
    private readonly IPrivacyExportService _exportService = Substitute.For<IPrivacyExportService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private ExportPersonalDataQueryHandler CreateSut()
    {
        return new ExportPersonalDataQueryHandler(
            _privacyRepository,
            _exportService,
            _currentUser,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_driver_exporting_own_data_succeeds_and_creates_zip_archive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fixedTime = new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);
        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _dateTimeProvider.UtcNow.Returns(fixedTime);

        var mockData = new DriverPersonalDataExportDto(
            new ExportMetadataDto(Guid.NewGuid(), userId, fixedTime, "IPP 6", "Nimpression Ops", "NZ"),
            new UserExportDto(userId, "driver@test.co.nz", "Driver John", "Driver", "Active", "en-NZ", fixedTime.AddMonths(-6), null),
            new DriverProfileExportDto(Guid.NewGuid(), "EMP-001", "Class 4", new DateOnly(2027, 1, 1), new DateOnly(2024, 1, 1), "Active", 30m, "NZD", 40m, "NZD", 0.8m, "NZD", "+6421111", "1 Queen St", "Jane"),
            [],
            [],
            [],
            [],
            [],
            []);

        _privacyRepository.MockExportData = mockData;
        var fakeZipBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 }; // ZIP header
        _exportService.CreateExportZipArchiveAsync(mockData, Arg.Any<CancellationToken>()).Returns(fakeZipBytes);

        var sut = CreateSut();
        var query = new ExportPersonalDataQuery(userId);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/zip");
        result.Value.ContentBytes.Should().Equal(fakeZipBytes);
        result.Value.FileName.Should().Contain("Driver_John");

        // Asserts DataSubjectRequest is logged
        _privacyRepository.StoredRequests.Should().ContainSingle(r =>
            r.SubjectUserId == userId &&
            r.Kind == DataSubjectRequestKind.Export &&
            r.Status == "Completed");
    }

    [Fact]
    public async Task Handle_driver_attempting_to_export_another_users_data_returns_forbidden_403()
    {
        // Arrange
        var currentDriverUserId = Guid.NewGuid();
        var targetVictimUserId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(currentDriverUserId);

        var sut = CreateSut();
        var query = new ExportPersonalDataQuery(targetVictimUserId);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert: 关键硬约束：导出的 zip 不许包含他人数据，越权必须返回 403 而非 404
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden_data_export");
    }

    [Fact]
    public async Task Handle_admin_can_export_any_drivers_data()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var fixedTime = new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

        _currentUser.Role.Returns(UserRole.Admin);
        _currentUser.UserId.Returns(adminUserId);
        _dateTimeProvider.UtcNow.Returns(fixedTime);

        var mockData = new DriverPersonalDataExportDto(
            new ExportMetadataDto(Guid.NewGuid(), driverUserId, fixedTime, "IPP 6", "Nimpression Ops", "NZ"),
            new UserExportDto(driverUserId, "driver2@test.co.nz", "Driver Two", "Driver", "Active", "en-NZ", fixedTime.AddMonths(-6), null),
            null,
            [],
            [],
            [],
            [],
            [],
            []);

        _privacyRepository.MockExportData = mockData;
        _exportService.CreateExportZipArchiveAsync(mockData, Arg.Any<CancellationToken>()).Returns([0x50, 0x4B]);

        var sut = CreateSut();
        var query = new ExportPersonalDataQuery(driverUserId);

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
