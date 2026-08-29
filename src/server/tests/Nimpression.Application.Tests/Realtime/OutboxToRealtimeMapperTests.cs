using System.Text.Json;
using FluentAssertions;
using Nimpression.Application.Features.Realtime.Common;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;
using Nimpression.Infrastructure.Realtime.Services;
using Xunit;

namespace Nimpression.Application.Tests.Realtime;

public sealed class OutboxToRealtimeMapperTests
{
    private readonly OutboxToRealtimeMapper _mapper = new();
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Map_JobTaskAssigned_ReturnsPureInvalidationSignal_AndRoutesToDriverAndDispatchers()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var occurredAt = FixedNow;
        var payload = JsonSerializer.Serialize(new
        {
            JobTaskId = taskId,
            DriverId = driverId,
            VehicleId = vehicleId,
            OccurredAt = occurredAt,
            // 即便原始载荷中有敏感业务数据，映射后也必须彻底丢弃
            CustomerAddress = "123 Secret St",
            Price = 150.00
        });

        var outbox = new OutboxMessage(Guid.NewGuid(), "JobTaskAssigned", payload, occurredAt);

        // Act
        var result = _mapper.Map(outbox);

        // Assert
        result.Message.Kind.Should().Be(RealtimeEventKinds.TaskAssigned);
        result.Message.EntityId.Should().Be(taskId);
        result.Message.OccurredAt.Should().Be(occurredAt);
        result.TargetDriverId.Should().Be(driverId);

        result.TargetGroups.Should().Contain(RealtimeGroupNames.Driver(driverId));
        result.TargetGroups.Should().Contain(RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()));
        result.TargetGroups.Should().Contain(RealtimeGroupNames.Role(UserRole.Admin.ToString()));
    }

    [Fact]
    public void Map_JobTaskAcknowledged_ReturnsPureInvalidationSignal_AndRoutesCorrectly()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var occurredAt = FixedNow;
        var payload = JsonSerializer.Serialize(new
        {
            JobTaskId = taskId,
            DriverId = driverId,
            OccurredAt = occurredAt
        });

        var outbox = new OutboxMessage(Guid.NewGuid(), "JobTaskAcknowledged", payload, occurredAt);

        // Act
        var result = _mapper.Map(outbox);

        // Assert
        result.Message.Kind.Should().Be(RealtimeEventKinds.TaskAcknowledged);
        result.Message.EntityId.Should().Be(taskId);
        result.TargetDriverId.Should().Be(driverId);
    }

    [Fact]
    public void Map_JobTaskCompleted_ReturnsPureInvalidationSignal_AndRoutesCorrectly()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var occurredAt = FixedNow;
        var payload = JsonSerializer.Serialize(new
        {
            JobTaskId = taskId,
            DriverId = driverId,
            OccurredAt = occurredAt
        });

        var outbox = new OutboxMessage(Guid.NewGuid(), "JobTaskCompleted", payload, occurredAt);

        // Act
        var result = _mapper.Map(outbox);

        // Assert
        result.Message.Kind.Should().Be(RealtimeEventKinds.TaskCompleted);
        result.Message.EntityId.Should().Be(taskId);
        result.TargetDriverId.Should().Be(driverId);
    }

    [Fact]
    public void Map_DriverDeactivated_ReturnsPureInvalidationSignal_AndRoutesCorrectly()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var occurredAt = FixedNow;
        var payload = JsonSerializer.Serialize(new
        {
            DriverId = driverId,
            OccurredAt = occurredAt
        });

        var outbox = new OutboxMessage(Guid.NewGuid(), "DriverDeactivated", payload, occurredAt);

        // Act
        var result = _mapper.Map(outbox);

        // Assert
        result.Message.Kind.Should().Be(RealtimeEventKinds.DriverDeactivated);
        result.Message.EntityId.Should().Be(driverId);
        result.TargetDriverId.Should().Be(driverId);
    }

    [Fact]
    public void Map_IncidentReported_ReturnsPureInvalidationSignal_AndRoutesCorrectly()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var occurredAt = FixedNow;
        var payload = JsonSerializer.Serialize(new
        {
            IncidentId = incidentId,
            DriverId = driverId,
            OccurredAt = occurredAt
        });

        var outbox = new OutboxMessage(Guid.NewGuid(), "IncidentReported", payload, occurredAt);

        // Act
        var result = _mapper.Map(outbox);

        // Assert
        result.Message.Kind.Should().Be(RealtimeEventKinds.IncidentReported);
        result.Message.EntityId.Should().Be(incidentId);
        result.TargetDriverId.Should().Be(driverId);
    }

    [Fact]
    public void Map_FineAccepted_ReturnsPureInvalidationSignal_AndRoutesCorrectly()
    {
        // Arrange
        var fineId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var occurredAt = FixedNow;
        var payload = JsonSerializer.Serialize(new
        {
            FineId = fineId,
            DriverId = driverId,
            OccurredAt = occurredAt
        });

        var outbox = new OutboxMessage(Guid.NewGuid(), "FineAccepted", payload, occurredAt);

        // Act
        var result = _mapper.Map(outbox);

        // Assert
        result.Message.Kind.Should().Be(RealtimeEventKinds.FineAccepted);
        result.Message.EntityId.Should().Be(fineId);
        result.TargetDriverId.Should().Be(driverId);
    }

    [Fact]
    public void Map_PayslipFinalised_RoutesToDriverAndAdminOnly_NeverToDispatcher()
    {
        // Arrange
        var payslipId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var occurredAt = FixedNow;
        var payload = JsonSerializer.Serialize(new
        {
            PayslipId = payslipId,
            DriverId = driverId,
            GrossPay = 2500.00m,
            OccurredAt = occurredAt
        });

        var outbox = new OutboxMessage(Guid.NewGuid(), "PayslipFinalised", payload, occurredAt);

        // Act
        var result = _mapper.Map(outbox);

        // Assert
        result.Message.Kind.Should().Be(RealtimeEventKinds.PayslipFinalised);
        result.Message.EntityId.Should().Be(payslipId);
        result.TargetGroups.Should().Contain(RealtimeGroupNames.Driver(driverId));
        result.TargetGroups.Should().Contain(RealtimeGroupNames.Role(UserRole.Admin.ToString()));
        result.TargetGroups.Should().NotContain(RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()));
    }

    [Fact]
    public void Map_NewsPublished_WithDifferentAudiences_RoutesToExpectedGroups()
    {
        var newsId = Guid.NewGuid();
        var now = FixedNow;

        // 1. All audience
        var allPayload = JsonSerializer.Serialize(new { NewsPostId = newsId, Audience = (int)NewsAudience.All, OccurredAt = now });
        var allRes = _mapper.Map("NewsPublished", allPayload, now);
        allRes.TargetGroups.Should().Contain(RealtimeGroupNames.All);

        // 2. Drivers only
        var drvPayload = JsonSerializer.Serialize(new { NewsPostId = newsId, Audience = (int)NewsAudience.Drivers, OccurredAt = now });
        var drvRes = _mapper.Map("NewsPublished", drvPayload, now);
        drvRes.TargetGroups.Should().Contain(RealtimeGroupNames.Role(UserRole.Driver.ToString()));
        drvRes.TargetGroups.Should().NotContain(RealtimeGroupNames.All);

        // 3. Dispatchers only
        var dspPayload = JsonSerializer.Serialize(new { NewsPostId = newsId, Audience = (int)NewsAudience.Dispatchers, OccurredAt = now });
        var dspRes = _mapper.Map("NewsPublished", dspPayload, now);
        dspRes.TargetGroups.Should().Contain(RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()));
        dspRes.TargetGroups.Should().Contain(RealtimeGroupNames.Role(UserRole.Admin.ToString()));
        dspRes.TargetGroups.Should().NotContain(RealtimeGroupNames.Role(UserRole.Driver.ToString()));
    }

    [Fact]
    public void Map_ServiceThresholdReached_RoutesToDispatcherAndAdmin()
    {
        var vehicleId = Guid.NewGuid();
        var now = FixedNow;
        var payload = JsonSerializer.Serialize(new { VehicleId = vehicleId, OccurredAt = now });

        var res = _mapper.Map("ServiceThresholdReached", payload, now);

        res.Message.Kind.Should().Be(RealtimeEventKinds.VehicleServiceThresholdReached);
        res.Message.EntityId.Should().Be(vehicleId);
        res.TargetGroups.Should().Contain(RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()));
        res.TargetGroups.Should().Contain(RealtimeGroupNames.Role(UserRole.Admin.ToString()));
    }
}
