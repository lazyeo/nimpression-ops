using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Commands.AnonymizeDriver;
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Application.Tests.Privacy.TestDoubles;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Privacy.Commands;

public sealed class AnonymizeDriverCommandHandlerTests
{
    private readonly FakePrivacyRepository _privacyRepository = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private AnonymizeDriverCommandHandler CreateSut()
    {
        return new AnonymizeDriverCommandHandler(_privacyRepository, _currentUser, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_admin_anonymizes_driver_and_verifies_invariants()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var fixedTime = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        _dateTimeProvider.UtcNow.Returns(fixedTime);
        _currentUser.Role.Returns(UserRole.Admin);

        _privacyRepository.MockAnonymizationResult = new AnonymizationResultDto(
            driverId,
            Guid.NewGuid(),
            fixedTime,
            $"Driver #{driverId.ToString("N")[..6]}",
            GrossPaySumBefore: 4500.00m,
            GrossPaySumAfter: 4500.00m,
            PayslipsCountBefore: 5,
            PayslipsCountAfter: 5,
            IncidentReportsCountBefore: 2,
            IncidentReportsCountAfter: 2,
            AuditEventsCountBefore: 12,
            AuditEventsCountAfter: 12);

        var sut = CreateSut();
        var command = new AnonymizeDriverCommand(driverId, "Driver resigned");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert: 关键纪律 N2.5：断言匿名化前后 SUM(GrossPay)、审计条数、事故条数完全不变
        result.IsSuccess.Should().BeTrue();
        result.Value.GrossPaySumBefore.Should().Be(result.Value.GrossPaySumAfter);
        result.Value.PayslipsCountBefore.Should().Be(result.Value.PayslipsCountAfter);
        result.Value.IncidentReportsCountBefore.Should().Be(result.Value.IncidentReportsCountAfter);
        result.Value.AnonymousIdentifier.Should().StartWith("Driver #");
    }

    [Fact]
    public async Task Handle_non_admin_returns_forbidden_403()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(Guid.NewGuid());

        var sut = CreateSut();
        var command = new AnonymizeDriverCommand(driverId);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert: N1.3 越权防护返回 403
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden_anonymization");
    }

    [Fact]
    public async Task Handle_corrupted_aggregation_invariant_returns_unprocessable_entity()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        _currentUser.Role.Returns(UserRole.Admin);
        _dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        // Mock a scenario where GrossPay was altered/corrupted during anonymization
        _privacyRepository.MockAnonymizationResult = new AnonymizationResultDto(
            driverId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Driver #corrupt",
            GrossPaySumBefore: 5000.00m,
            GrossPaySumAfter: 0.00m, // Corrupted: sum became 0
            PayslipsCountBefore: 4,
            PayslipsCountAfter: 4,
            IncidentReportsCountBefore: 1,
            IncidentReportsCountAfter: 1,
            AuditEventsCountBefore: 5,
            AuditEventsCountAfter: 5);

        var sut = CreateSut();
        var command = new AnonymizeDriverCommand(driverId);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert: 必须拦截并报错，绝不静默放过破坏财务历史的数据损坏
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("anonymization_integrity_violation");
    }
}
