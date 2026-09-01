namespace Nimpression.Application.Features.Privacy.DTOs;

/// <summary>
/// 个人数据全量导出载荷（AC N2.4 / NZ Privacy Act 2020 IPP 6 查阅权）。
/// 聚合司机本人的用户档案、联系方式明文、班次记录、派单历史、工资单与明细、事故报告、罚单记录与合规同意记录。
/// </summary>
public sealed record DriverPersonalDataExportDto(
    ExportMetadataDto Metadata,
    UserExportDto User,
    DriverProfileExportDto? Driver,
    IReadOnlyList<ShiftExportDto> Shifts,
    IReadOnlyList<JobTaskExportDto> Tasks,
    IReadOnlyList<PayslipExportDto> Payslips,
    IReadOnlyList<IncidentExportDto> Incidents,
    IReadOnlyList<FineExportDto> Fines,
    IReadOnlyList<ConsentRecordExportDto> Consents);

public sealed record ExportMetadataDto(
    Guid ExportRequestId,
    Guid SubjectUserId,
    DateTimeOffset ExportedAt,
    string LegalBasis,
    string OrganizationName,
    string Jurisdiction);

public sealed record UserExportDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    string Locale,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record DriverProfileExportDto(
    Guid Id,
    string EmployeeNo,
    string LicenceClass,
    DateOnly LicenceExpiry,
    DateOnly HiredOn,
    string Status,
    decimal HourlyRateAmount,
    string HourlyRateCurrency,
    decimal PerTripRateAmount,
    string PerTripRateCurrency,
    decimal PerKmRateAmount,
    string PerKmRateCurrency,
    string Phone,
    string Address,
    string EmergencyContact);

public sealed record ShiftExportDto(
    Guid Id,
    DateTimeOffset ClockInAt,
    DateTimeOffset? ClockOutAt,
    int BreakMinutes,
    string Status,
    string? Note,
    decimal WorkHoursDecimal);

public sealed record JobTaskExportDto(
    Guid Id,
    string Ref,
    string Title,
    string? Description,
    DateTimeOffset ScheduledFor,
    string Priority,
    string Status,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    decimal? PlannedDistanceKm,
    decimal? ActualDistanceKm);

public sealed record PayslipExportDto(
    Guid Id,
    Guid PayPeriodId,
    string PayPeriodName,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string BasisUsed,
    decimal GrossPayAmount,
    string Currency,
    decimal HoursBasedGross,
    decimal TripBasedGross,
    bool MinimumWageTopUp,
    DateTimeOffset CalculatedAt,
    DateTimeOffset? FinalisedAt,
    IReadOnlyList<PayslipLineExportDto> Lines);

public sealed record PayslipLineExportDto(
    Guid Id,
    string Basis,
    string Kind,
    string Description,
    decimal? Hours,
    decimal? DistanceKm,
    int? Qty,
    decimal RateAmount,
    decimal Amount);

public sealed record IncidentExportDto(
    Guid Id,
    Guid VehicleId,
    DateTimeOffset OccurredAt,
    string Location,
    string Severity,
    string Description,
    string? ThirdPartyInfo,
    IReadOnlyList<string> PhotoKeys);

public sealed record FineExportDto(
    Guid Id,
    string NoticeNumber,
    DateOnly InfringementDate,
    decimal Amount,
    string Status,
    string? OffenceDescription);

public sealed record ConsentRecordExportDto(
    string PolicyVersion,
    DateTimeOffset ConsentedAt,
    string? IpAddress,
    string? UserAgent);
