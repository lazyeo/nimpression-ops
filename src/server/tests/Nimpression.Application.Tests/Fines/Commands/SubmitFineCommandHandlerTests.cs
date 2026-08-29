using System.Text;
using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Application.Features.Fines.Abstractions;
using Nimpression.Application.Features.Fines.Commands.SubmitFine;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Fines.Commands;

public sealed class SubmitFineCommandHandlerTests
{
    private readonly IFineRepository _fineRepository = Substitute.For<IFineRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IObjectStorageService _storageService = Substitute.For<IObjectStorageService>();

    private SubmitFineCommandHandler CreateSut()
    {
        return new SubmitFineCommandHandler(_fineRepository, _unitOfWork, _currentUser, _storageService);
    }

    [Fact]
    public async Task Handle_driver_submits_fine_for_self_success()
    {
        // Arrange
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _fineRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(driverId);
        _fineRepository.VehicleExistsAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(true);

        var command = new SubmitFineCommand(
            DriverId: driverId,
            VehicleId: vehicleId,
            IssuedOn: new DateOnly(2026, 8, 20),
            Authority: "NZ Police",
            Reference: "REF-001",
            Amount: 150m,
            Currency: "NZD",
            Reason: "Speeding",
            TicketPhotoKey: "fines/photo1.jpg");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        await _fineRepository.Received(1).AddAsync(
            Arg.Is<Fine>(f =>
                f.DriverId == driverId &&
                f.VehicleId == vehicleId &&
                f.Authority == "NZ Police" &&
                f.Status == FineStatus.Submitted &&
                f.TicketPhotoKey == "fines/photo1.jpg"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_driver_submits_with_photo_stream_uploads_to_storage()
    {
        // Arrange (F8.1: 照片存对象存储，DB 只存 key)
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _fineRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(driverId);
        _fineRepository.VehicleExistsAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(true);

        const string generatedKey = "fines/uploaded_key.jpg";
        _storageService.UploadAsync(
            "nimpression-media",
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            "image/jpeg",
            Arg.Any<CancellationToken>()).Returns(generatedKey);

        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-content"));

        var command = new SubmitFineCommand(
            DriverId: null,
            VehicleId: vehicleId,
            IssuedOn: new DateOnly(2026, 8, 20),
            Authority: "Auckland Transport",
            Reference: "AT-999",
            Amount: 80m,
            Currency: "NZD",
            Reason: "Bus lane",
            TicketPhotoKey: null,
            PhotoStream: memoryStream,
            PhotoFileName: "ticket.jpg",
            PhotoContentType: "image/jpeg");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _storageService.Received(1).UploadAsync(
            "nimpression-media",
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            "image/jpeg",
            Arg.Any<CancellationToken>());

        await _fineRepository.Received(1).AddAsync(
            Arg.Is<Fine>(f => f.TicketPhotoKey == generatedKey),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_driver_submits_for_other_driver_returns_403_forbidden()
    {
        // Arrange
        var sut = CreateSut();
        var ownDriverId = Guid.NewGuid();
        var otherDriverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _fineRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(ownDriverId);

        var command = new SubmitFineCommand(
            DriverId: otherDriverId,
            VehicleId: Guid.NewGuid(),
            IssuedOn: new DateOnly(2026, 8, 20),
            Authority: "NZ Police",
            Reference: "REF-002",
            Amount: 150m,
            Currency: "NZD",
            Reason: "Speeding");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");
    }

    [Fact]
    public async Task Handle_vehicle_not_found_returns_404_not_found()
    {
        // Arrange
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _fineRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(driverId);
        _fineRepository.VehicleExistsAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(false);

        var command = new SubmitFineCommand(
            DriverId: driverId,
            VehicleId: vehicleId,
            IssuedOn: new DateOnly(2026, 8, 20),
            Authority: "NZ Police",
            Reference: "REF-003",
            Amount: 150m,
            Currency: "NZD",
            Reason: "Speeding");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("vehicle_not_found");
    }
}
