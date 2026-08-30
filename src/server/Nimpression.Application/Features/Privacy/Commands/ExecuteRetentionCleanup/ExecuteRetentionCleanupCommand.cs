using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;

namespace Nimpression.Application.Features.Privacy.Commands.ExecuteRetentionCleanup;

/// <summary>
/// 执行数据保留策略清理任务（AC N2.3）。
/// 关键纪律：默认必须为 Dry-Run 模式（Execute = false），仅在显式传入 Execute = true 时才执行物理删除与脱敏。
/// </summary>
public sealed record ExecuteRetentionCleanupCommand(
    DateTimeOffset? ReferenceDate = null,
    bool Execute = false) : IRequest<Result<RetentionCleanupReportDto>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "PrivacyRetentionPolicy";
    public Guid? AuditEntityId => null;
    public string AuditAction => Execute ? "ExecuteRetentionCleanupLive" : "ExecuteRetentionCleanupDryRun";
}

public sealed class ExecuteRetentionCleanupCommandValidator : AbstractValidator<ExecuteRetentionCleanupCommand>
{
    public ExecuteRetentionCleanupCommandValidator()
    {
        // ReferenceDate 允许为空（为空时自动注入 IDateTimeProvider 时钟）
    }
}

public sealed class ExecuteRetentionCleanupCommandHandler(
    IPrivacyRepository privacyRepository,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<ExecuteRetentionCleanupCommand, Result<RetentionCleanupReportDto>>
{
    public async Task<Result<RetentionCleanupReportDto>> Handle(
        ExecuteRetentionCleanupCommand request,
        CancellationToken cancellationToken)
    {
        // 禁止直接调用 DateTimeOffset.UtcNow，统一从 IDateTimeProvider 或请求参数获取
        var effectiveDate = request.ReferenceDate ?? dateTimeProvider.UtcNow;

        var report = await privacyRepository.ExecuteRetentionCleanupAsync(
            effectiveDate,
            request.Execute,
            cancellationToken);

        return Result<RetentionCleanupReportDto>.Success(report);
    }
}
