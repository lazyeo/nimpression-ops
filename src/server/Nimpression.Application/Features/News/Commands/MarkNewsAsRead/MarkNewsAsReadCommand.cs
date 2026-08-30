using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.News.Commands.MarkNewsAsRead;

/// <summary>
/// 记录新闻公告已读回执命令（F10.2）。
/// </summary>
public sealed record MarkNewsAsReadCommand(
    Guid NewsPostId,
    Guid? UserId = null) : IRequest<Result>, ICommandMarker;
