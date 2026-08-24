using System.Text;
using FluentAssertions;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Application.Features.Identity.Queries.ExportAuditLogs;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Queries;

public class ExportAuditLogsQueryHandlerTests
{
    private readonly IIdentityRepository _identityRepository = Substitute.For<IIdentityRepository>();
    private readonly ExportAuditLogsQueryHandler _handler;

    public ExportAuditLogsQueryHandlerTests()
    {
        _handler = new ExportAuditLogsQueryHandler(_identityRepository);
    }

    [Fact]
    public async Task Handle_WithLogs_ReturnsValidCsvFileResult()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

        var logs = new List<AuditEventDto>
        {
            new(logId, "User.Deactivate", "User", "123", occurredAt, actorId, UserRole.Admin, "{\"status\":\"Active\"}", "{\"status\":\"Inactive\"}", "127.0.0.1", "Browser")
        };

        _identityRepository.QueryAllAuditLogsAsync(
            null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(logs);

        var query = new ExportAuditLogsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ContentType.Should().Be("text/csv; charset=utf-8");
        result.Value.FileName.Should().StartWith("audit-logs-").And.EndWith(".csv");

        var csvText = Encoding.UTF8.GetString(result.Value.Bytes);
        csvText.Should().Contain("Id,OccurredAt,ActorUserId,ActorRole,Action,EntityType,EntityId,IpAddress,UserAgent,BeforeJson,AfterJson");
        csvText.Should().Contain("User.Deactivate");
        csvText.Should().Contain("User");
        csvText.Should().Contain("123");
    }

    [Fact]
    public async Task Handle_WithCommasAndQuotesInJson_EscapesCsvProperly()
    {
        // Arrange
        var logs = new List<AuditEventDto>
        {
            new(Guid.NewGuid(), "Action,With,Commas", "Entity", "1", DateTimeOffset.UtcNow, null, null, "{\"name\":\"John, Doe\"}", "{\"quote\":\"hello \\\"world\\\"\"}", "127.0.0.1", "Agent")
        };

        _identityRepository.QueryAllAuditLogsAsync(
            null, null, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns(logs);

        var query = new ExportAuditLogsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var csvText = Encoding.UTF8.GetString(result.Value.Bytes);
        csvText.Should().Contain("\"Action,With,Commas\"");
        csvText.Should().Contain("\"{\"\"name\"\":\"\"John, Doe\"\"}\"");
    }
}
