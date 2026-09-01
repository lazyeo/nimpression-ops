namespace Nimpression.Application.Features.Privacy.DTOs;

/// <summary>
/// 数据保留策略清理执行报告（AC N2.3）。
/// 包含执行时间基准、dry-run 标识、各类过期记录的清理数量与明细。
/// </summary>
public sealed record RetentionCleanupReportDto(
    DateTimeOffset ReferenceDate,
    bool IsDryRun,
    int ShiftGpsCoordinatesPurgedCount,
    int ExpiredRefreshTokensPurgedCount,
    int ExpiredEmailLogsPurgedCount,
    DateTimeOffset ProcessedAt,
    IReadOnlyList<string> ActionSummaries);
