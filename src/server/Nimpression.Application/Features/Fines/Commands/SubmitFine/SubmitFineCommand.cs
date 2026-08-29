using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Fines.Commands.SubmitFine;

/// <summary>
/// 提交交通罚单命令（F8.1）。
/// 司机可为自己提交，管理员/调度员可代为提交。支持直接携带对象存储 Key 或流式上传照片。
/// </summary>
public sealed record SubmitFineCommand(
    Guid? DriverId,
    Guid VehicleId,
    DateOnly IssuedOn,
    string Authority,
    string Reference,
    decimal Amount,
    string? Currency,
    string Reason,
    string? TicketPhotoKey = null,
    Stream? PhotoStream = null,
    string? PhotoFileName = null,
    string? PhotoContentType = null) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public Guid? CreatedId { get; set; }

    public string AuditEntityType => "Fine";
    public Guid? AuditEntityId => CreatedId;
    public string AuditAction => "SubmitFine";
}
