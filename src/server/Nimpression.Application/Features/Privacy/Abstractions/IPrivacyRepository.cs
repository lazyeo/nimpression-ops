using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Privacy.Abstractions;

/// <summary>
/// 隐私合规仓储契约（AC N2.1 - N2.7）。
/// 集中承载个人数据导出、保留策略清理、司机不可逆匿名化与隐私同意记录操作。
/// </summary>
public interface IPrivacyRepository
{
    /// <summary>
    /// 获取指定用户的全量个人数据（查阅权 IPP 6）。
    /// </summary>
    Task<DriverPersonalDataExportDto?> GetDriverPersonalDataAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定用户最新特定类型的隐私请求。
    /// </summary>
    Task<DataSubjectRequest?> GetLatestRequestAsync(Guid userId, DataSubjectRequestKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增主体隐私权利请求。
    /// </summary>
    Task AddDataSubjectRequestAsync(DataSubjectRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行数据保留策略清理任务（AC N2.3）。
    /// 默认必须支持 dry-run 模式：仅计算并报告将删除/清理的记录数；只有当 execute = true 时才执行实际更新/删除。
    /// </summary>
    Task<RetentionCleanupReportDto> ExecuteRetentionCleanupAsync(DateTimeOffset referenceDate, bool execute, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行离职司机数据不可逆匿名化（AC N2.5）。
    /// 将可识别 PII 字段替换为不可逆占位符，保留所有业务关联与数值，断言前后工资单总金额、行数、审计与事故记录数完全不变。
    /// </summary>
    Task<AnonymizationResultDto> AnonymizeDriverAsync(Guid driverId, DateTimeOffset referenceDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定用户在特定隐私声明版本下的同意状态。
    /// </summary>
    Task<PrivacyConsentDto> GetPrivacyConsentStatusAsync(Guid userId, string policyVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录用户隐私声明同意记录（AC N2.7）。
    /// </summary>
    Task RecordPrivacyConsentAsync(Guid userId, string policyVersion, DateTimeOffset consentedAt, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取隐私权利请求列表。
    /// </summary>
    Task<IReadOnlyList<DataSubjectRequestDto>> GetDataSubjectRequestsAsync(Guid? userId, CancellationToken cancellationToken = default);
}
