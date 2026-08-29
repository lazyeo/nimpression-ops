using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Application.Features.Fines.Abstractions;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Fines.Queries.GetFinePhotoUrl;

public sealed class GetFinePhotoUrlQueryHandler(
    IFineRepository fineRepository,
    ICurrentUser currentUser,
    IObjectStorageService storageService) : IRequestHandler<GetFinePhotoUrlQuery, Result<string>>
{
    private const string MediaBucketName = "nimpression-media";

    public async Task<Result<string>> Handle(GetFinePhotoUrlQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        var fine = await fineRepository.GetByIdAsync(request.FineId, cancellationToken);
        if (fine is null)
        {
            return Error.NotFound("fine_not_found", $"Fine with ID '{request.FineId}' was not found.");
        }

        // F8.4 核心验收标准：越权取他人罚单照片必须 403 Forbidden（非 404）
        if (currentUser.Role == UserRole.Driver)
        {
            var ownDriverId = await fineRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue || fine.DriverId != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers are only permitted to access photos of their own fines.");
            }
        }

        if (string.IsNullOrWhiteSpace(fine.TicketPhotoKey))
        {
            return Error.NotFound("ticket_photo_not_found", "No ticket photo attached to this fine.");
        }

        // F8.4: 短时效签名 URL（≤15min）
        var presignedUrl = await storageService.GetPresignedUrlAsync(
            MediaBucketName,
            fine.TicketPhotoKey,
            TimeSpan.FromMinutes(15),
            cancellationToken);

        return presignedUrl;
    }
}
