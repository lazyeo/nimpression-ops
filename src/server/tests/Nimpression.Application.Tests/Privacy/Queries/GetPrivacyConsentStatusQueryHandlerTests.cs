using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Queries.GetPrivacyConsentStatus;
using Nimpression.Application.Tests.Privacy.TestDoubles;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Privacy.Queries;

public sealed class GetPrivacyConsentStatusQueryHandlerTests
{
    private readonly FakePrivacyRepository _privacyRepository = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    [Fact]
    public async Task Handle_returns_consent_status_for_authenticated_user()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId.Returns(userId);
        var consentedAt = new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

        _privacyRepository.RecordedConsents.Add((userId, "2026.1", consentedAt, "127.0.0.1", "Agent/1.0"));

        var sut = new GetPrivacyConsentStatusQueryHandler(_privacyRepository, _currentUser);
        var query = new GetPrivacyConsentStatusQuery("2026.1");

        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasConsented.Should().BeTrue();
        result.Value.PolicyVersion.Should().Be("2026.1");
        result.Value.ConsentedAt.Should().Be(consentedAt);
        result.Value.Title.Should().NotBeNullOrWhiteSpace();
        result.Value.ContentMarkdown.Should().Contain("Privacy Act 2020");
    }

    [Fact]
    public async Task Handle_unauthenticated_returns_unauthorized_401()
    {
        _currentUser.UserId.Returns((Guid?)null);

        var sut = new GetPrivacyConsentStatusQueryHandler(_privacyRepository, _currentUser);
        var query = new GetPrivacyConsentStatusQuery("2026.1");

        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
    }
}
