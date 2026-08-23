using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Drivers.Commands.CreateDriver;

/// <summary>
/// 创建司机命令（F2.1）。由系统管理员调用。
/// </summary>
public sealed class CreateDriverCommand : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public Guid? Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string EmployeeNo { get; init; } = string.Empty;
    public string LicenceClass { get; init; } = string.Empty;
    public DateOnly LicenceExpiry { get; init; }
    public decimal HourlyRateAmount { get; init; }
    public string HourlyRateCurrency { get; init; } = "NZD";
    public decimal PerTripRateAmount { get; init; }
    public string PerTripRateCurrency { get; init; } = "NZD";
    public decimal PerKmRateAmount { get; init; }
    public string PerKmRateCurrency { get; init; } = "NZD";
    public string Phone { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string EmergencyContact { get; init; } = string.Empty;
    public DateOnly HiredOn { get; init; }
    public List<Guid>? AreaIds { get; init; }

    internal Guid? CreatedId { get; set; }

    public string AuditEntityType => "Driver";
    public Guid? AuditEntityId => CreatedId ?? Id;
    public string AuditAction => "CreateDriver";
}
