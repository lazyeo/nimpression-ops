using FluentValidation;

namespace Nimpression.Application.Features.News.Commands.MarkNewsAsRead;

/// <summary>
/// 已读回执命令校验器。
/// </summary>
public sealed class MarkNewsAsReadCommandValidator : AbstractValidator<MarkNewsAsReadCommand>
{
    public MarkNewsAsReadCommandValidator()
    {
        RuleFor(x => x.NewsPostId)
            .NotEmpty().WithMessage("News post ID is required.");
    }
}
