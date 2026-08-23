using Nimpression.Domain.Common;

namespace Nimpression.Domain.Events;

/// <summary>
/// 司机已被停用事件。
/// </summary>
public sealed record DriverDeactivated(
    Guid DriverId,
    Guid UserId,
    DateTimeOffset OccurredAt) : IDomainEvent;
