namespace Nimpression.Application.Features.Dispatch.DTOs;

/// <summary>
/// 派发任务分页结果包装。
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Math.Max(1, PageSize));
}
