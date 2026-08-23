using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Application.Features.Vehicles.Common;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Vehicles.Commands.CreateVehicle;

/// <summary>
/// 创建车辆命令处理器。
/// </summary>
public sealed class CreateVehicleCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateVehicleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        Rego rego;
        try
        {
            rego = new Rego(request.Rego);
        }
        catch (Exception ex)
        {
            return Error.Validation("invalid_rego", ex.Message);
        }

        if (await vehicleRepository.ExistsByRegoAsync(rego, cancellationToken))
        {
            return Error.Conflict("vehicle_rego_conflict", $"Vehicle with registration plate '{rego.Value}' already exists.");
        }

        var vehicle = new Vehicle(
            Guid.NewGuid(),
            rego,
            request.Make,
            request.Model,
            request.Year,
            request.VinEnc,
            new Kilometres(request.OdometerKm),
            new Kilometres(request.ServiceIntervalKm),
            request.LastServiceOdometerKm.HasValue ? new Kilometres(request.LastServiceOdometerKm.Value) : null,
            request.WofExpiry,
            request.CofExpiry,
            request.InsuranceExpiry,
            request.Status);

        try
        {
            await vehicleRepository.AddVehicleAsync(vehicle, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            return Error.Conflict("vehicle_rego_conflict", $"Vehicle with registration plate '{rego.Value}' already exists.");
        }

        return vehicle.Id;
    }
}
