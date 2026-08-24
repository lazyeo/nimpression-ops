using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.DTOs;

namespace Nimpression.Application.Features.Drivers.Commands.UploadDriverAvatar;

/// <summary>
/// 上传司机头像命令（F2.2）。
/// </summary>
public sealed record UploadDriverAvatarCommand(
    Guid DriverId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileLength) : IRequest<Result<UploadAvatarResultDto>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Driver";
    public Guid? AuditEntityId => DriverId;
    public string AuditAction => "UploadAvatar";
}
