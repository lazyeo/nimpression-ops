using Nimpression.Domain.Common;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Events;

/// <summary>
/// 交通罚单已确认接受事件。
/// </summary>
public sealed record FineAccepted(
    Guid FineId,
    Guid DriverId,
    Guid VehicleId,
    Money Amount,
    DateTimeOffset OccurredAt) : IDomainEvent;
