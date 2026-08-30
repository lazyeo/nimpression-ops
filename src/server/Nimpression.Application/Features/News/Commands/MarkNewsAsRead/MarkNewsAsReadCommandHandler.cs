using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.Abstractions;
using Nimpression.Application.Features.News.Common;
using Nimpression.Domain.Entities.Communications;

namespace Nimpression.Application.Features.News.Commands.MarkNewsAsRead;

/// <summary>
/// 记录新闻已读回执 Handler（F10.2）。
/// 遵循无“先查后写”原则，通过捕获唯一约束异常实现并发幂等处理。
/// </summary>
public sealed class MarkNewsAsReadCommandHandler(
    INewsRepository newsRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<MarkNewsAsReadCommand, Result>
{
    public async Task<Result> Handle(MarkNewsAsReadCommand request, CancellationToken cancellationToken)
    {
        var targetUserId = request.UserId ?? currentUser.UserId;
        if (!targetUserId.HasValue || targetUserId.Value == Guid.Empty)
        {
            return Result.Failure(Error.Unauthorized("unauthorized", "User is not authenticated."));
        }

        var post = await newsRepository.GetByIdAsync(request.NewsPostId, cancellationToken);
        if (post == null)
        {
            return Result.Failure(Error.NotFound("news_not_found", "News post was not found."));
        }

        var receipt = new NewsReadReceipt(
            Guid.NewGuid(),
            request.NewsPostId,
            targetUserId.Value,
            dateTimeProvider.UtcNow);

        try
        {
            await newsRepository.AddReadReceiptAsync(receipt, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            // 唯一约束冲突（PostgreSQL 23505）：同一人重复打开幂等返回成功，严禁“先查后写”
            return Result.Success();
        }
    }
}
