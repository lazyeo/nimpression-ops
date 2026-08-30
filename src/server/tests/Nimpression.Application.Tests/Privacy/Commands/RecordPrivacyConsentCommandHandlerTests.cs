using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Commands.RecordPrivacyConsent;
using Nimpression.Application.Tests.Privacy.TestDoubles;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Privacy.Commands;

public sealed class RecordPrivacyConsentCommandHandlerTests
{
    private readonly FakePrivacyRepository _privacyRepository = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private RecordPrivacyConsentCommandHandler CreateSut()
    {
        return new RecordPrivacyConsentCommandHandler(_privacyRepository, _currentUser, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_records_consent_with_version_timestamp_and_ip()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fixedTime = new DateTimeOffset(2026, 8, 30, 8, 30, 0, TimeSpan.Zero);
        _currentUser.UserId.Returns(userId);
        _currentUser.IpAddress.Returns("202.89.4.12");
        _currentUser.UserAgent.Returns("Mozilla/5.0 Chrome/128");
        _dateTimeProvider.UtcNow.Returns(fixedTime);

        var sut = CreateSut();
        var command = new RecordPrivacyConsentCommand("2026.2");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasConsented.Should().BeTrue();
        result.Value.PolicyVersion.Should().Be("2026.2");
        result.Value.ConsentedAt.Should().Be(fixedTime);
        result.Value.ConsentedIpAddress.Should().Be("202.89.4.12");

        _privacyRepository.RecordedConsents.Should().ContainSingle(c =>
            c.UserId == userId &&
            c.PolicyVersion == "2026.2" &&
            c.ConsentedAt == fixedTime &&
            c.IpAddress == "202.89.4.12");
    }

    [Fact]
    public async Task Handle_unauthenticated_user_returns_unauthorized_401()
    {
        // Arrange
        _currentUser.UserId.Returns((Guid?)null);

        var sut = CreateSut();
        var command = new RecordPrivacyConsentCommand("2026.1");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
    }
}
