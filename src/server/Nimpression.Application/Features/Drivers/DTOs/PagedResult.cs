namespace Nimpression.Application.Features.Drivers.DTOs;

/// <summary>
/// 通用分页结果容器。
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 20));
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
