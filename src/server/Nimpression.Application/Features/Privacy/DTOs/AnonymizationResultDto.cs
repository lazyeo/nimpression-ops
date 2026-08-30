namespace Nimpression.Application.Features.Privacy.DTOs;

/// <summary>
/// 司机数据不可逆匿名化执行结果（AC N2.5）。
/// 包含不可逆占位符标识、匿名化前后工资单总金额、工资单行数、审计事件数及事故记录数不变性断言指标。
/// </summary>
public sealed record AnonymizationResultDto(
    Guid DriverId,
    Guid UserId,
    DateTimeOffset AnonymizedAt,
    string AnonymousIdentifier,
    decimal GrossPaySumBefore,
    decimal GrossPaySumAfter,
    int PayslipsCountBefore,
    int PayslipsCountAfter,
    int IncidentReportsCountBefore,
    int IncidentReportsCountAfter,
    int AuditEventsCountBefore,
    int AuditEventsCountAfter);
