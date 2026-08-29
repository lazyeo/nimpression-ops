using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Timesheets.Commands.AdminCorrectShift;

/// <summary>
/// 管理员更正打卡记录命令（F6.4）。
/// 必须填写更正理由（缺理由 422），更正前后数据全量留存审计。
/// </summary>
public sealed record AdminCorrectShiftCommand(
    Guid ShiftId,
    DateTimeOffset NewClockInAt,
    DateTimeOffset? NewClockOutAt = null,
    int NewBreakMinutes = 0,
    string Reason = "") : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "ShiftEntry";
    public Guid? AuditEntityId => ShiftId;
    public string AuditAction => "AdminCorrectShift";
}
