using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.Queries.GetDriverById;

/// <summary>
/// 按 ID 获取司机详情查询处理器（F2.1 / N1.3）。
/// </summary>
public sealed class GetDriverByIdQueryHandler(
    IDriverRepository driverRepository,
    ICurrentUser currentUser,
    IObjectStorageService? storageService = null,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<GetDriverByIdQuery, Result<DriverDetailDto>>
{
    private const string MediaBucketName = "nimpression-media";

    public async Task<Result<DriverDetailDto>> Handle(
        GetDriverByIdQuery request,
        CancellationToken cancellationToken)
    {
        var referenceDate = dateTimeProvider?.NzToday ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var detail = await driverRepository.GetDriverDetailByIdAsync(request.DriverId, referenceDate, cancellationToken);
        if (detail is null)
        {
            return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId}' was not found.");
        }

        // 越权防护：司机只能查看本人的档案，查询他人档案直接返回 403（N1.3 / F7.10）
        if (currentUser.Role == UserRole.Driver && currentUser.UserId != detail.UserId)
        {
            return Error.Forbidden("forbidden", "Drivers are not authorized to view other drivers' profiles.");
        }

        // 若存在头像 Key，生成短时效签名 URL（≤15分钟）
        if (!string.IsNullOrWhiteSpace(detail.AvatarKey) && storageService is not null)
        {
            try
            {
                var presignedUrl = await storageService.GetPresignedUrlAsync(
                    MediaBucketName,
                    detail.AvatarKey,
                    TimeSpan.FromMinutes(15),
                    cancellationToken);

                detail = detail with { AvatarUrl = presignedUrl };
            }
            catch
            {
                // URL 生成失败不阻塞基本信息返回
            }
        }

        return detail;
    }
}
