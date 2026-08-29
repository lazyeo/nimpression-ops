using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Application.Features.Fines.Abstractions;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Fines.Commands.SubmitFine;

/// <summary>
/// 提交交通罚单命令处理器（F8.1）。
/// </summary>
public sealed class SubmitFineCommandHandler(
    IFineRepository fineRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IObjectStorageService? storageService = null) : IRequestHandler<SubmitFineCommand, Result<Guid>>
{
    private const string MediaBucketName = "nimpression-media";

    public async Task<Result<Guid>> Handle(SubmitFineCommand request, CancellationToken cancellationToken)
    {
        Guid targetDriverId;

        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var ownDriverId = await fineRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue)
            {
                return Error.NotFound("driver_not_found", "Driver profile for current user was not found.");
            }

            if (request.DriverId.HasValue && request.DriverId.Value != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers can only submit fines for themselves.");
            }

            targetDriverId = ownDriverId.Value;
        }
        else if (currentUser.Role is UserRole.Admin or UserRole.Dispatcher)
        {
            if (!request.DriverId.HasValue || request.DriverId.Value == Guid.Empty)
            {
                return Error.Validation("driver_id_required", "DriverId is mandatory for management fine submission.");
            }

            if (!await fineRepository.DriverExistsAsync(request.DriverId.Value, cancellationToken))
            {
                return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId.Value}' was not found.");
            }

            targetDriverId = request.DriverId.Value;
        }
        else
        {
            return Error.Unauthorized("unauthorized", "User is not authorized to submit fines.");
        }

        if (!await fineRepository.VehicleExistsAsync(request.VehicleId, cancellationToken))
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle with ID '{request.VehicleId}' was not found.");
        }

        var photoKey = request.TicketPhotoKey;

        // 如果上传了文件流，保存至对象存储，DB 仅保存 Key（F8.1）
        if (request.PhotoStream is not null && storageService is not null)
        {
            var ext = Path.GetExtension(request.PhotoFileName) switch
            {
                { Length: > 0 } e => e.ToLowerInvariant(),
                _ => ".jpg"
            };

            var generatedKey = $"fines/{Guid.NewGuid():N}{ext}";
            var contentType = string.IsNullOrWhiteSpace(request.PhotoContentType)
                ? "image/jpeg"
                : request.PhotoContentType;

            photoKey = await storageService.UploadAsync(
                MediaBucketName,
                generatedKey,
                request.PhotoStream,
                contentType,
                cancellationToken);
        }

        var fineId = Guid.NewGuid();
        request.CreatedId = fineId;

        var fine = new Fine(
            fineId,
            targetDriverId,
            request.VehicleId,
            request.IssuedOn,
            request.Authority,
            request.Reference,
            new Money(request.Amount, request.Currency ?? "NZD"),
            request.Reason,
            photoKey);

        await fineRepository.AddAsync(fine, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return fineId;
    }
}
