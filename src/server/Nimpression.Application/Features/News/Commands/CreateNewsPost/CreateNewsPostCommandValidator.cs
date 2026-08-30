using FluentValidation;

namespace Nimpression.Application.Features.News.Commands.CreateNewsPost;

/// <summary>
/// 发布新闻公告命令校验器（硬约束：双语正文必填，缺一 422）。
/// </summary>
public sealed class CreateNewsPostCommandValidator : AbstractValidator<CreateNewsPostCommand>
{
    public CreateNewsPostCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("News title is required.")
            .MaximumLength(200).WithMessage("News title must not exceed 200 characters.");

        RuleFor(x => x.BodyEn)
            .NotEmpty().WithMessage("English body is required.");

        RuleFor(x => x.BodyZh)
            .NotEmpty().WithMessage("Chinese body is required.");

        RuleFor(x => x.Audience)
            .IsInEnum().WithMessage("A valid target audience must be specified.");
    }
}
