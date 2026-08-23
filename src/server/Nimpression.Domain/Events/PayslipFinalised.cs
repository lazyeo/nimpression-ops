using Nimpression.Domain.Common;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Events;

/// <summary>
/// 工资单已定版事件。
/// </summary>
public sealed record PayslipFinalised(
    Guid PayslipId,
    Guid PayPeriodId,
    Guid DriverId,
    Money GrossPay,
    DateTimeOffset OccurredAt) : IDomainEvent;
