using FluentValidation;

namespace Nimpression.Application.Features.Drivers.Commands.UploadDriverAvatar;

/// <summary>
/// 上传司机头像命令校验器。
/// </summary>
public sealed class UploadDriverAvatarCommandValidator : AbstractValidator<UploadDriverAvatarCommand>
{
    public UploadDriverAvatarCommandValidator()
    {
        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("DriverId is required.");

        RuleFor(x => x.FileStream)
            .NotNull().WithMessage("FileStream cannot be null.");

        RuleFor(x => x.FileLength)
            .GreaterThan(0).WithMessage("Uploaded file is empty.")
            .LessThanOrEqualTo(2 * 1024 * 1024).WithMessage("Avatar file size cannot exceed 2MB.");
    }
}
