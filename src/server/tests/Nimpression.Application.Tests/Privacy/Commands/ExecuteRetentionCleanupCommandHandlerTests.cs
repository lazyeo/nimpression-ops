using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Privacy.Commands.ExecuteRetentionCleanup;
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Application.Tests.Privacy.TestDoubles;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Privacy.Commands;

public sealed class ExecuteRetentionCleanupCommandHandlerTests
{
    private readonly FakePrivacyRepository _privacyRepository = new();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private ExecuteRetentionCleanupCommandHandler CreateSut()
    {
        return new ExecuteRetentionCleanupCommandHandler(_privacyRepository, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_default_command_defaults_to_dry_run_mode()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);
        _dateTimeProvider.UtcNow.Returns(fixedTime);
        var sut = CreateSut();

        var command = new ExecuteRetentionCleanupCommand(); // Default Execute is false

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert: 关键纪律 N2.3：默认必须为 Dry-Run，绝不直接真删
        result.IsSuccess.Should().BeTrue();
        result.Value.IsDryRun.Should().BeTrue();
        result.Value.ReferenceDate.Should().Be(fixedTime);
        result.Value.ActionSummaries.Should().Contain(s => s.Contains("[DRY-RUN]"));
    }

    [Fact]
    public async Task Handle_explicit_execute_true_runs_live_cleanup()
    {
        // Arrange
        var explicitDate = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var sut = CreateSut();

        var command = new ExecuteRetentionCleanupCommand(explicitDate, Execute: true);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsDryRun.Should().BeFalse();
        result.Value.ReferenceDate.Should().Be(explicitDate);
        result.Value.ActionSummaries.Should().Contain(s => s.Contains("[LIVE]"));
    }

    [Fact]
    public void Command_has_auditable_marker_and_correct_action_names()
    {
        var dryRunCmd = new ExecuteRetentionCleanupCommand(Execute: false);
        dryRunCmd.AuditEntityType.Should().Be("PrivacyRetentionPolicy");
        dryRunCmd.AuditAction.Should().Be("ExecuteRetentionCleanupDryRun");

        var liveCmd = new ExecuteRetentionCleanupCommand(Execute: true);
        liveCmd.AuditEntityType.Should().Be("PrivacyRetentionPolicy");
        liveCmd.AuditAction.Should().Be("ExecuteRetentionCleanupLive");
    }
}
