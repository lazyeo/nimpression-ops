using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.Abstractions;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.Commands.CreateNewsPost;

/// <summary>
/// 发布新闻公告 Handler（F10.1 / F10.3）。
/// </summary>
public sealed class CreateNewsPostCommandHandler(
    INewsRepository newsRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<CreateNewsPostCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateNewsPostCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        if (currentUser.Role != UserRole.Admin)
        {
            return Error.Forbidden("forbidden", "Only administrators can publish news.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Error.Unprocessable("news_title_required", "News title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.BodyEn) || string.IsNullOrWhiteSpace(request.BodyZh))
        {
            return Error.Unprocessable(
                "bilingual_body_required",
                "Both English and Chinese body contents are required.");
        }

        var post = new NewsPost(
            Guid.NewGuid(),
            currentUser.UserId.Value,
            request.Title,
            request.BodyEn,
            request.BodyZh,
            request.Audience,
            dateTimeProvider.UtcNow,
            request.Pinned,
            true);

        await newsRepository.AddNewsPostAsync(post, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return post.Id;
    }
}
