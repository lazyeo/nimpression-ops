using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Application.Features.Fines.Abstractions;
using Nimpression.Application.Features.Fines.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Fines.Queries.GetFineById;

public sealed class GetFineByIdQueryHandler(
    IFineRepository fineRepository,
    ICurrentUser currentUser,
    IObjectStorageService? storageService = null) : IRequestHandler<GetFineByIdQuery, Result<FineDetailDto>>
{
    private const string MediaBucketName = "nimpression-media";

    public async Task<Result<FineDetailDto>> Handle(GetFineByIdQuery request, CancellationToken cancellationToken)
    {
        var detail = await fineRepository.GetFineDetailByIdAsync(request.FineId, cancellationToken);
        if (detail is null)
        {
            return Error.NotFound("fine_not_found", $"Fine with ID '{request.FineId}' was not found.");
        }

        // IDOR 越权校验：司机只能查看本人的罚单，查看他人罚单必须返回 403 Forbidden（F8.4 / N1.3）
        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var ownDriverId = await fineRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue || detail.DriverId != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers are only permitted to view their own fines.");
            }
        }

        // F8.4: 照片走短时效签名 URL（≤15min）
        if (!string.IsNullOrWhiteSpace(detail.TicketPhotoKey) && storageService is not null)
        {
            try
            {
                var presignedUrl = await storageService.GetPresignedUrlAsync(
                    MediaBucketName,
                    detail.TicketPhotoKey,
                    TimeSpan.FromMinutes(15),
                    cancellationToken);

                detail = detail with { TicketPhotoUrl = presignedUrl };
            }
            catch
            {
                // URL 生成失败不阻塞详情返回
            }
        }

        return detail;
    }
}
