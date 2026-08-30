using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Application.Features.Notifications.Compliance.Commands.TriggerComplianceScan;

/// <summary>
/// 触发车辆合规到期预警扫描命令（F3.5 / F11）。
/// </summary>
public sealed record TriggerComplianceScanCommand : IRequest<Result<int>>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "Compliance.ScanTriggered";
    public string AuditEntityType => "ComplianceScan";
    public Guid? AuditEntityId => null;
}

public sealed class TriggerComplianceScanCommandHandler(
    IComplianceExpiryScanner complianceExpiryScanner) : IRequestHandler<TriggerComplianceScanCommand, Result<int>>
{
    public async Task<Result<int>> Handle(TriggerComplianceScanCommand request, CancellationToken cancellationToken)
    {
        return await complianceExpiryScanner.ScanAndNotifyAsync(cancellationToken);
    }
}
