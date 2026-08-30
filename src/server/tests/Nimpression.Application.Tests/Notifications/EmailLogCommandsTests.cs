using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.EmailLogs.Commands.ResendEmail;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Tests.Notifications;

public class EmailLogCommandsTests
{
    private readonly IEmailLogRepository _logRepo = Substitute.For<IEmailLogRepository>();
    private readonly IEmailTemplateRepository _templateRepo = Substitute.For<IEmailTemplateRepository>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task ResendEmail_WhenEmailAlreadySent_ReturnsUnprocessableError()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var email = new EmailAddress("partner@example.com");
        var log = new EmailLog(logId, "SERVICE_DUE_REMINDER", email, "Service Due", "Trigger", "CORR-01");
        log.RecordSuccess(DateTimeOffset.UtcNow);

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>()).Returns(log);

        var handler = new ResendEmailCommandHandler(_logRepo, _templateRepo, _emailSender, _dateTimeProvider, _unitOfWork);

        // Act
        var result = await handler.Handle(new ResendEmailCommand(logId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("email_already_sent");
        await _emailSender.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendEmail_WhenFailedEmailResentSuccessfully_UpdatesStatusToSent()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var email = new EmailAddress("partner@example.com");
        var log = new EmailLog(logId, "SERVICE_DUE_REMINDER", email, "Service Due", "Trigger", "CORR-01");
        log.RecordFailure("SMTP Connection Timed Out");

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>()).Returns(log);
        _dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero));

        var handler = new ResendEmailCommandHandler(_logRepo, _templateRepo, _emailSender, _dateTimeProvider, _unitOfWork);

        // Act
        var result = await handler.Handle(new ResendEmailCommand(logId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        log.Status.Should().Be("Sent");
        log.SentAt.Should().Be(new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero));
        await _emailSender.Received(1).SendEmailAsync("partner@example.com", "Service Due", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendEmail_WhenEmailSendingThrows_RecordsFailureAndReturnsErrorWithoutSilentDegradation()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var email = new EmailAddress("partner@example.com");
        var log = new EmailLog(logId, "SERVICE_DUE_REMINDER", email, "Service Due", "Trigger", "CORR-01");
        log.RecordFailure("SMTP Connection Timed Out");

        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>()).Returns(log);
        _emailSender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Mail server down"));

        var handler = new ResendEmailCommandHandler(_logRepo, _templateRepo, _emailSender, _dateTimeProvider, _unitOfWork);

        // Act
        var result = await handler.Handle(new ResendEmailCommand(logId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be("email_send_failed");
        log.Status.Should().Be("Failed");
        log.Attempts.Should().Be(2);
        log.LastError.Should().Contain("Mail server down");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
