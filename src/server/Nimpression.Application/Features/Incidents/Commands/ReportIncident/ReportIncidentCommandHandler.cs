using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Incidents.Abstractions;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Incidents.Commands.ReportIncident;

/// <summary>
/// 事故上报命令处理器（F9.1 / F9.2 / F9.3）。
/// </summary>
public sealed class ReportIncidentCommandHandler(
    IIncidentRepository incidentRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<ReportIncidentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReportIncidentCommand request, CancellationToken cancellationToken)
    {
        Guid targetDriverId;

        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var ownDriverId = await incidentRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue)
            {
                return Error.NotFound("driver_not_found", "Driver profile was not found.");
            }

            if (request.DriverId.HasValue && request.DriverId.Value != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers can only report incidents for themselves.");
            }

            targetDriverId = ownDriverId.Value;
        }
        else if (currentUser.Role is UserRole.Admin or UserRole.Dispatcher)
        {
            if (!request.DriverId.HasValue || request.DriverId.Value == Guid.Empty)
            {
                return Error.Validation("driver_id_required", "DriverId is mandatory for management incident reporting.");
            }

            if (!await incidentRepository.DriverExistsAsync(request.DriverId.Value, cancellationToken))
            {
                return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId.Value}' was not found.");
            }

            targetDriverId = request.DriverId.Value;
        }
        else
        {
            return Error.Unauthorized("unauthorized", "User is not authorized to report incidents.");
        }

        if (!await incidentRepository.VehicleExistsAsync(request.VehicleId, cancellationToken))
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle with ID '{request.VehicleId}' was not found.");
        }

        var incidentId = Guid.NewGuid();
        request.CreatedId = incidentId;

        var incident = new IncidentReport(
            incidentId,
            targetDriverId,
            request.VehicleId,
            request.OccurredAt,
            request.Location,
            request.Severity,
            request.Description,
            request.PhotoKeys,
            request.ThirdPartyInfo);

        // F9.2: 严重度 ≥ Moderate 自动发领域事件通知保险方并记 InsurerNotifiedAt；Minor 不自动发
        if (incident.ShouldNotifyInsurer)
        {
            var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
            incident.MarkInsurerNotified(now);
        }
        else
        {
            // Minor 级别不通知保险方，清除领域事件以避免写入发件箱 (Outbox)
            incident.ClearDomainEvents();
        }

        await incidentRepository.AddAsync(incident, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return incidentId;
    }
}
