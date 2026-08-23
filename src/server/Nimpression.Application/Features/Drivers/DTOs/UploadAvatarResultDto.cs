namespace Nimpression.Application.Features.Drivers.DTOs;

/// <summary>
/// 头像上传结果 DTO（F2.2）。
/// </summary>
public sealed record UploadAvatarResultDto(
    string AvatarKey,
    string AvatarUrl);
