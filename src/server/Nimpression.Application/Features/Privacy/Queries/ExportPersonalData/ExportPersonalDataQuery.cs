using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Privacy.Queries.ExportPersonalData;

public sealed record ExportPersonalDataQuery(Guid TargetUserId) : IRequest<Result<PersonalDataExportFileDto>>;

public sealed record PersonalDataExportFileDto(
    string FileName,
    string ContentType,
    byte[] ContentBytes,
    DriverPersonalDataExportDto DataPayload);

public sealed class ExportPersonalDataQueryHandler(
    IPrivacyRepository privacyRepository,
    IPrivacyExportService exportService,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<ExportPersonalDataQuery, Result<PersonalDataExportFileDto>>
{
    public async Task<Result<PersonalDataExportFileDto>> Handle(
        ExportPersonalDataQuery request,
        CancellationToken cancellationToken)
    {
        // 越权防护（N1.3 / AC N2.4）：司机只能导出本人的数据，越权访问必须返回 403 而非 404
        if (currentUser.Role == UserRole.Driver && currentUser.UserId != request.TargetUserId)
        {
            return Error.Forbidden(
                "forbidden_data_export",
                "Drivers are strictly prohibited from exporting personal data belonging to other users.");
        }

        var exportData = await privacyRepository.GetDriverPersonalDataAsync(request.TargetUserId, cancellationToken);
        if (exportData is null)
        {
            return Error.NotFound(
                "user_not_found",
                $"User with ID '{request.TargetUserId}' was not found.");
        }

        // 生成 Zip 归档文件
        var zipBytes = await exportService.CreateExportZipArchiveAsync(exportData, cancellationToken);

        // 记录或标记 DataSubjectRequest 实体
        var dsr = new DataSubjectRequest(
            Guid.NewGuid(),
            request.TargetUserId,
            DataSubjectRequestKind.Export,
            dateTimeProvider.UtcNow);

        var exportKey = $"exports/{request.TargetUserId:N}_{dateTimeProvider.UtcNow:yyyyMMddHHmmss}.zip";
        dsr.Complete(exportKey, dateTimeProvider.UtcNow);
        await privacyRepository.AddDataSubjectRequestAsync(dsr, cancellationToken);

        var fileName = $"privacy_export_{exportData.User.DisplayName.Replace(' ', '_')}_{dateTimeProvider.UtcNow:yyyyMMdd}.zip";
        var result = new PersonalDataExportFileDto(
            fileName,
            "application/zip",
            zipBytes,
            exportData);

        return Result<PersonalDataExportFileDto>.Success(result);
    }
}
