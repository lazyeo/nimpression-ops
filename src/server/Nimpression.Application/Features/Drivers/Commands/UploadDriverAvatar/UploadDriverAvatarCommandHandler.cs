using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.Commands.UploadDriverAvatar;

/// <summary>
/// 上传司机头像命令处理器（F2.2）。
/// </summary>
public sealed class UploadDriverAvatarCommandHandler(
    IDriverRepository driverRepository,
    IObjectStorageService storageService,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<UploadDriverAvatarCommand, Result<UploadAvatarResultDto>>
{
    private const string MediaBucketName = "nimpression-media";

    public async Task<Result<UploadAvatarResultDto>> Handle(
        UploadDriverAvatarCommand request,
        CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId}' was not found.");
        }

        // 越权防护：如果是 Driver 角色，只能上传本人头像（N1.3 / F2.2）
        if (currentUser.Role == UserRole.Driver && currentUser.UserId != driver.UserId)
        {
            return Error.Forbidden("forbidden", "Drivers can only upload their own avatar.");
        }

        // 魔数与大小服务端校验（F2.2）
        var validation = ImageValidator.Validate(request.FileStream, request.FileLength);
        if (!validation.IsValid)
        {
            if (validation.ErrorCode == "file_too_large")
            {
                return Error.Unprocessable("file_too_large", validation.ErrorMessage ?? "Image exceeds 2MB limit.");
            }

            return Error.UnsupportedMediaType("unsupported_media_type", validation.ErrorMessage ?? "Unsupported image format. Only JPEG and PNG are allowed.");
        }

        var extension = validation.Extension ?? ".jpg";
        var contentType = validation.ContentType ?? "image/jpeg";
        var key = $"avatars/{driver.UserId:N}_{Guid.NewGuid():N}{extension}";

        await storageService.UploadAsync(
            MediaBucketName,
            key,
            request.FileStream,
            contentType,
            cancellationToken);

        var user = await driverRepository.GetUserByIdAsync(driver.UserId, cancellationToken);
        if (user is not null)
        {
            user.UpdateProfile(user.DisplayName, key, user.Locale);
            driverRepository.UpdateUser(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 生成 ≤15 分钟的短时效签名 URL
        var presignedUrl = await storageService.GetPresignedUrlAsync(
            MediaBucketName,
            key,
            TimeSpan.FromMinutes(15),
            cancellationToken);

        return new UploadAvatarResultDto(key, presignedUrl);
    }
}
