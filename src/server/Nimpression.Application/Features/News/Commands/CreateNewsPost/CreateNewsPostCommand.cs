using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.Commands.CreateNewsPost;

/// <summary>
/// 发布新闻公告命令（F10.1）。
/// </summary>
public sealed record CreateNewsPostCommand(
    string Title,
    string BodyEn,
    string BodyZh,
    NewsAudience Audience,
    bool Pinned = false) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "NewsPost";
    public Guid? AuditEntityId => null;
    public string AuditAction => "PublishNews";
}
