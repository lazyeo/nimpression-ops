using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Fines.Abstractions;
using Nimpression.Application.Features.Fines.Commands.AcceptFine;
using Nimpression.Application.Features.Fines.Commands.DisputeFine;
using Nimpression.Application.Features.Fines.Commands.StartFineReview;
using Nimpression.Application.Features.Fines.Commands.WaiveFine;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Fines.Commands;

public sealed class ReviewFineCommandHandlerTests
{
    private readonly IFineRepository _fineRepository = Substitute.For<IFineRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedTime = new(2026, 8, 20, 14, 0, 0, TimeSpan.FromHours(12));

    public ReviewFineCommandHandlerTests()
    {
        _currentUser.Role.Returns(UserRole.Admin);
        _currentUser.UserId.Returns(_adminUserId);
        _dateTimeProvider.UtcNow.Returns(_fixedTime);
    }

    private static Fine CreateFine()
    {
        return new Fine(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 10),
            "NZ Police",
            "REF-REV-001",
            new Money(120m),
            "Speeding");
    }

    [Fact]
    public async Task StartReview_from_Submitted_status_succeeds()
    {
        // Arrange (F8.2: Submitted -> UnderReview)
        var fine = CreateFine();
        _fineRepository.GetByIdAsync(fine.Id, Arg.Any<CancellationToken>()).Returns(fine);

        var handler = new StartFineReviewCommandHandler(_fineRepository, _unitOfWork, _currentUser, _dateTimeProvider);
        var command = new StartFineReviewCommand(fine.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        fine.Status.Should().Be(FineStatus.UnderReview);
        fine.ReviewedByUserId.Should().Be(_adminUserId);
        fine.ReviewedAt.Should().Be(_fixedTime);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReview_from_AlreadyUnderReview_returns_422_unprocessable()
    {
        // Arrange (F8.2: 非法流转返回 422)
        var fine = CreateFine();
        fine.StartReview(_adminUserId, _fixedTime.AddHours(-1));
        _fineRepository.GetByIdAsync(fine.Id, Arg.Any<CancellationToken>()).Returns(fine);

        var handler = new StartFineReviewCommandHandler(_fineRepository, _unitOfWork, _currentUser, _dateTimeProvider);
        var command = new StartFineReviewCommand(fine.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("invalid_fine_transition");
    }

    [Fact]
    public async Task AcceptFine_from_UnderReview_succeeds_and_adds_FineAccepted_domain_event()
    {
        // Arrange (F8.2 / F8.3: UnderReview -> Accepted, 触发 FineAccepted 事件)
        var fine = CreateFine();
        fine.StartReview(_adminUserId, _fixedTime.AddHours(-1));
        _fineRepository.GetByIdAsync(fine.Id, Arg.Any<CancellationToken>()).Returns(fine);

        var handler = new AcceptFineCommandHandler(_fineRepository, _unitOfWork, _currentUser, _dateTimeProvider);
        var command = new AcceptFineCommand(fine.Id, "Driver acknowledged");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        fine.Status.Should().Be(FineStatus.Accepted);
        fine.ReviewNote.Should().Be("Driver acknowledged");
        fine.DomainEvents.Should().ContainSingle(e => e is FineAccepted);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptFine_from_Submitted_returns_422_unprocessable()
    {
        // Arrange: 未进入 UnderReview 直接 Accept 必须 422
        var fine = CreateFine();
        _fineRepository.GetByIdAsync(fine.Id, Arg.Any<CancellationToken>()).Returns(fine);

        var handler = new AcceptFineCommandHandler(_fineRepository, _unitOfWork, _currentUser, _dateTimeProvider);
        var command = new AcceptFineCommand(fine.Id, "Direct accept");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("invalid_fine_transition");
    }

    [Fact]
    public async Task DisputeFine_from_UnderReview_succeeds()
    {
        // Arrange (F8.2: UnderReview -> Disputed)
        var fine = CreateFine();
        fine.StartReview(_adminUserId, _fixedTime.AddHours(-1));
        _fineRepository.GetByIdAsync(fine.Id, Arg.Any<CancellationToken>()).Returns(fine);

        var handler = new DisputeFineCommandHandler(_fineRepository, _unitOfWork, _currentUser, _dateTimeProvider);
        var command = new DisputeFineCommand(fine.Id, "Incorrect speed camera calibration");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        fine.Status.Should().Be(FineStatus.Disputed);
        fine.ReviewNote.Should().Be("Incorrect speed camera calibration");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisputeFine_empty_review_note_returns_validation_error()
    {
        // Arrange
        var fine = CreateFine();
        fine.StartReview(_adminUserId, _fixedTime.AddHours(-1));
        _fineRepository.GetByIdAsync(fine.Id, Arg.Any<CancellationToken>()).Returns(fine);

        var handler = new DisputeFineCommandHandler(_fineRepository, _unitOfWork, _currentUser, _dateTimeProvider);
        var command = new DisputeFineCommand(fine.Id, "");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task WaiveFine_from_UnderReview_succeeds()
    {
        // Arrange (F8.2: UnderReview -> Waived)
        var fine = CreateFine();
        fine.StartReview(_adminUserId, _fixedTime.AddHours(-1));
        _fineRepository.GetByIdAsync(fine.Id, Arg.Any<CancellationToken>()).Returns(fine);

        var handler = new WaiveFineCommandHandler(_fineRepository, _unitOfWork, _currentUser, _dateTimeProvider);
        var command = new WaiveFineCommand(fine.Id, "Signage obstructed");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        fine.Status.Should().Be(FineStatus.Waived);
        fine.ReviewNote.Should().Be("Signage obstructed");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
