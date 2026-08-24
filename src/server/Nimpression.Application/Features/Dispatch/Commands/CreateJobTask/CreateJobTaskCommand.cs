using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Dispatch.Commands.CreateJobTask;

/// <summary>
/// 创建派发任务命令（F5.1 / F4.3）。
/// </summary>
public sealed record CreateJobTaskCommand(
    string? Ref,
    string Title,
    Guid AreaId,
    DateTimeOffset ScheduledFor,
    TaskPriority Priority = TaskPriority.Medium,
    string? Description = null,
    decimal? PlannedDistanceKm = null,
    Guid? DriverId = null,
    Guid? VehicleId = null,
    bool OverrideAreaWarning = false) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "JobTask";
    public Guid? AuditEntityId => null;
    public string AuditAction => "CreateJobTask";
}
