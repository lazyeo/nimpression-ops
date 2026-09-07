using FluentAssertions;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Behaviors;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Commands.CreateJobTask;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Common.Behaviors;

public sealed class AuditBehaviorTests
{
    private readonly IAuditSink _auditSink = Substitute.For<IAuditSink>();

    [Fact]
    public async Task Handle_CreateJobTaskCommand_Success_RecordsAuditWithResolvedEntityIdAndAfterJson()
    {
        // Arrange
        var behavior = new AuditBehavior<CreateJobTaskCommand, Result<Guid>>(_auditSink);
        var expectedTaskId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var scheduledFor = new DateTimeOffset(2026, 9, 8, 8, 30, 0, TimeSpan.FromHours(12));

        var command = new CreateJobTaskCommand(
            "TSK-20260908-001",
            "Urgent Medical Delivery",
            areaId,
            scheduledFor,
            TaskPriority.Urgent,
            "Critical hospital supplies",
            25.5m);

        // Act: simulates handler execution setting CreatedId and returning Result<Guid>
        var result = await behavior.Handle(
            command,
            (ct) =>
            {
                command.CreatedId = expectedTaskId;
                return Task.FromResult(Result<Guid>.Success(expectedTaskId));
            },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedTaskId);

        await _auditSink.Received(1).RecordAsync(
            Arg.Is("JobTask"),
            Arg.Is<Guid?>(id => id == expectedTaskId),
            Arg.Is("CreateJobTask"),
            Arg.Is<string?>(b => b == null),
            Arg.Is<string?>(a => a != null
                                && a.Contains("Urgent Medical Delivery")
                                && a.Contains("TSK-20260908-001")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreateCommand_WithNullAuditEntityId_ExtractsEntityIdFromResult()
    {
        // Arrange: command where AuditEntityId property returns null, but Result contains Guid
        var behavior = new AuditBehavior<AnonymousCreateCommand, Result<Guid>>(_auditSink);
        var expectedId = Guid.NewGuid();
        var command = new AnonymousCreateCommand("Test Item");

        // Act
        var result = await behavior.Handle(
            command,
            (ct) => Task.FromResult(Result<Guid>.Success(expectedId)),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _auditSink.Received(1).RecordAsync(
            Arg.Is("AnonymousEntity"),
            Arg.Is<Guid?>(id => id == expectedId),
            Arg.Is("CreateAnonymous"),
            Arg.Is<string?>(b => b == null),
            Arg.Is<string?>(a => a != null && a.Contains("Test Item")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommandWithSensitiveFields_RedactsSensitiveKeysInAfterJson()
    {
        // Arrange
        var behavior = new AuditBehavior<SensitiveCommand, Result>(_auditSink);
        var entityId = Guid.NewGuid();
        var command = new SensitiveCommand(entityId, "sensitive-password-123!", "jwt-secret-value", "public-data");

        // Act
        var result = await behavior.Handle(
            command,
            (ct) => Task.FromResult(Result.Success()),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _auditSink.Received(1).RecordAsync(
            Arg.Is("SecureEntity"),
            Arg.Is<Guid?>(id => id == entityId),
            Arg.Is("UpdateSecure"),
            Arg.Is<string?>(b => b == null),
            Arg.Is<string?>(a => a != null
                                && a.Contains("[REDACTED]")
                                && a.Contains("public-data")
                                && !a.Contains("sensitive-password-123!")
                                && !a.Contains("jwt-secret-value")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedCommand_DoesNotRecordAudit()
    {
        // Arrange
        var behavior = new AuditBehavior<CreateJobTaskCommand, Result<Guid>>(_auditSink);
        var command = new CreateJobTaskCommand(
            "TSK-FAIL",
            "Failed Task",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        // Act
        var result = await behavior.Handle(
            command,
            (ct) => Task.FromResult(Result<Guid>.Failure(Error.Validation("invalid", "Task invalid"))),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _auditSink.DidNotReceiveWithAnyArgs().RecordAsync(
            default!, default, default!, default, default, default);
    }

    private sealed record AnonymousCreateCommand(string Name) : IRequest<Result<Guid>>, IAuditableCommand
    {
        public string AuditEntityType => "AnonymousEntity";
        public Guid? AuditEntityId => null;
        public string AuditAction => "CreateAnonymous";
    }

    private sealed record SensitiveCommand(
        Guid EntityId,
        string Password,
        string Secret,
        string PublicInfo) : IRequest<Result>, IAuditableCommand
    {
        public string AuditEntityType => "SecureEntity";
        public Guid? AuditEntityId => EntityId;
        public string AuditAction => "UpdateSecure";
    }
}
