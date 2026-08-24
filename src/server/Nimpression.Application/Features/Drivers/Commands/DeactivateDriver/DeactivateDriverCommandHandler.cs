using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.Commands.DeactivateDriver;

/// <summary>
/// 停用司机命令处理器。
/// </summary>
public sealed class DeactivateDriverCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<DeactivateDriverCommand, Result>
{
    public async Task<Result> Handle(DeactivateDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId}' was not found.");
        }

        var deactivatedAt = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        driver.Deactivate(deactivatedAt);
        driverRepository.UpdateDriver(driver);

        var user = await driverRepository.GetUserByIdAsync(driver.UserId, cancellationToken);
        if (user is not null)
        {
            user.SetStatus(UserStatus.Inactive);
            driverRepository.UpdateUser(user);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
