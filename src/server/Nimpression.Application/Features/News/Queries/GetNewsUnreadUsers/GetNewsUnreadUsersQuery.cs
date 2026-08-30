using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.DTOs;

namespace Nimpression.Application.Features.News.Queries.GetNewsUnreadUsers;

/// <summary>
/// 查询新闻公告未读人员名单 Query（F10.2）。
/// </summary>
public sealed record GetNewsUnreadUsersQuery(Guid NewsPostId) : IRequest<Result<IReadOnlyList<UnreadUserDto>>>;
