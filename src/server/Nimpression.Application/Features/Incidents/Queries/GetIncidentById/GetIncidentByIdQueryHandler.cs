using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Application.Features.Incidents.Abstractions;
using Nimpression.Application.Features.Incidents.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Incidents.Queries.GetIncidentById;

public sealed class GetIncidentByIdQueryHandler(
    IIncidentRepository incidentRepository,
    ICurrentUser currentUser,
    IObjectStorageService? storageService = null) : IRequestHandler<GetIncidentByIdQuery, Result<IncidentReportDetailDto>>
{
    private const string MediaBucketName = "nimpression-media";

    public async Task<Result<IncidentReportDetailDto>> Handle(GetIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        var detail = await incidentRepository.GetIncidentDetailByIdAsync(request.IncidentId, cancellationToken);
        if (detail is null)
        {
            return Error.NotFound("incident_not_found", $"Incident report with ID '{request.IncidentId}' was not found.");
        }

        // IDOR 越权校验：司机只能查看本人的事故报告，查看他人报告返回 403 Forbidden（N1.3）
        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var ownDriverId = await incidentRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue || detail.DriverId != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers are only permitted to view their own incident reports.");
            }
        }

        // F8.4 / F9.1: 多图预签名 URL（≤15min）
        if (detail.PhotoKeys.Count > 0 && storageService is not null)
        {
            var urls = new List<string>();
            foreach (var key in detail.PhotoKeys)
            {
                try
                {
                    var presignedUrl = await storageService.GetPresignedUrlAsync(
                        MediaBucketName,
                        key,
                        TimeSpan.FromMinutes(15),
                        cancellationToken);

                    urls.Add(presignedUrl);
                }
                catch
                {
                    // URL 生成失败不中断
                }
            }

            detail = detail with { PhotoUrls = urls };
        }

        return detail;
    }
}
