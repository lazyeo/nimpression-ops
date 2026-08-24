using System.Text;
using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.Commands.UploadDriverAvatar;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Commands;

public sealed class UploadDriverAvatarCommandHandlerTests
{
    private readonly IDriverRepository _driverRepository = Substitute.For<IDriverRepository>();
    private readonly IObjectStorageService _storageService = Substitute.For<IObjectStorageService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UploadDriverAvatarCommandHandler CreateSut()
    {
        return new UploadDriverAvatarCommandHandler(
            _driverRepository,
            _storageService,
            _currentUser,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_with_valid_jpeg_uploads_and_returns_presigned_url()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            userId,
            "DRV-001",
            "Class 4",
            new DateOnly(2027, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "phone",
            "addr",
            "emg",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        var user = new User(
            userId,
            new EmailAddress("driver@nimpression.co.nz"),
            "hash",
            UserRole.Driver,
            "Driver Name",
            "en-NZ");

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);
        _driverRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _currentUser.Role.Returns(UserRole.Admin);

        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        using var stream = new MemoryStream(jpegBytes);

        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<string>(1));
        _storageService.GetPresignedUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("http://minio:9000/presigned-url");

        var command = new UploadDriverAvatarCommand(
            driverId,
            stream,
            "avatar.jpg",
            "image/jpeg",
            jpegBytes.Length);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AvatarKey.Should().StartWith("avatars/");
        result.Value.AvatarUrl.Should().Be("http://minio:9000/presigned-url");

        user.AvatarKey.Should().Be(result.Value.AvatarKey);
        _driverRepository.Received(1).UpdateUser(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_with_disguised_file_returns_unsupported_media_type_415()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            userId,
            "DRV-001",
            "Class 4",
            new DateOnly(2027, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "phone",
            "addr",
            "emg",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);
        _currentUser.Role.Returns(UserRole.Admin);

        var fakeBytes = Encoding.UTF8.GetBytes("<?php phpinfo(); ?>");
        using var stream = new MemoryStream(fakeBytes);

        var command = new UploadDriverAvatarCommand(
            driverId,
            stream,
            "malicious.jpg",
            "image/jpeg",
            fakeBytes.Length);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnsupportedMediaType);
        result.Error.Code.Should().Be("unsupported_media_type");
    }

    [Fact]
    public async Task Handle_driver_uploading_other_drivers_avatar_returns_forbidden_403()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            userId,
            "DRV-001",
            "Class 4",
            new DateOnly(2027, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "phone",
            "addr",
            "emg",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);
        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(otherUserId); // 不是本司机

        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        using var stream = new MemoryStream(jpegBytes);

        var command = new UploadDriverAvatarCommand(
            driverId,
            stream,
            "avatar.jpg",
            "image/jpeg",
            jpegBytes.Length);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");
    }
}
