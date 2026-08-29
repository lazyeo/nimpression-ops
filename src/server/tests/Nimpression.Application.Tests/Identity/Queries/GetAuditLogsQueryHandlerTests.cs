using FluentAssertions;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Application.Features.Identity.Queries.GetAuditLogs;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Queries;

public class GetAuditLogsQueryHandlerTests
{
    private readonly IIdentityRepository _identityRepository = Substitute.For<IIdentityRepository>();
    private readonly GetAuditLogsQueryHandler _handler;

    public GetAuditLogsQueryHandlerTests()
    {
        _handler = new GetAuditLogsQueryHandler(_identityRepository);
    }

    [Fact]
    public async Task Handle_WithValidFilter_DelegatesToRepositoryAndReturnsPagedResult()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var baseNow = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var fromUtc = baseNow.AddDays(-7);
        var toUtc = baseNow;

        var items = new List<AuditEventDto>
        {
            new(Guid.NewGuid(), "User.Lockout", "User", actorId.ToString(), baseNow, actorId, UserRole.Admin, null, "{}", "127.0.0.1", "Agent")
        };
        var pagedResult = new PagedResult<AuditEventDto>(items, 1, 1, 20);

        _identityRepository.QueryAuditLogsAsync(
            actorId, "User", actorId.ToString(), "User.Lockout", fromUtc, toUtc, 1, 20, Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var query = new GetAuditLogsQuery(actorId, "User", actorId.ToString(), "User.Lockout", fromUtc, toUtc, 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalCount.Should().Be(1);
        result.Value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithInvalidPageOrPageSize_NormalizesDefaults()
    {
        // Arrange
        var pagedResult = new PagedResult<AuditEventDto>(new List<AuditEventDto>(), 0, 1, 20);
        _identityRepository.QueryAuditLogsAsync(
            null, null, null, null, null, null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var query = new GetAuditLogsQuery(Page: -5, PageSize: 500);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _identityRepository.Received(1).QueryAuditLogsAsync(
            null, null, null, null, null, null, 1, 20, Arg.Any<CancellationToken>());
    }
}
