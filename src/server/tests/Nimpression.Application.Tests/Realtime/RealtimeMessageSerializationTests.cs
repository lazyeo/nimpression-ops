using System.Text.Json;
using FluentAssertions;
using Nimpression.Application.Features.Realtime.DTOs;
using Xunit;

namespace Nimpression.Application.Tests.Realtime;

public sealed class RealtimeMessageSerializationTests
{
    [Fact]
    public void RealtimeMessage_SerializesToExpectedCamelCaseJson_WithoutBusinessPayload()
    {
        // Arrange
        var entityId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var occurredAt = new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
        var msg = new RealtimeMessage("task.assigned", entityId, occurredAt);

        // Act
        var json = JsonSerializer.Serialize(msg);

        // Assert
        json.Should().Contain("\"kind\":\"task.assigned\"");
        json.Should().Contain("\"entityId\":\"11111111-2222-3333-4444-555555555555\"");
        json.Should().Contain("\"occurredAt\":\"2026-08-24T10:00:00+00:00\"");

        // 验证反序列化
        var deserialized = JsonSerializer.Deserialize<RealtimeMessage>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Kind.Should().Be("task.assigned");
        deserialized.EntityId.Should().Be(entityId);
        deserialized.OccurredAt.Should().Be(occurredAt);
    }
}
