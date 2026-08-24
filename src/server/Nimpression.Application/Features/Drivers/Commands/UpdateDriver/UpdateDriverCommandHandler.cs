using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Drivers.Commands.UpdateDriver;

/// <summary>
/// 管理员更新司机信息命令处理器。
/// </summary>
public sealed class UpdateDriverCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateDriverCommand, Result>
{
    public async Task<Result> Handle(UpdateDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId}' was not found.");
        }

        var user = await driverRepository.GetUserByIdAsync(driver.UserId, cancellationToken);

        var phoneEnc = request.Phone.StartsWith("ENC(", StringComparison.Ordinal)
            ? request.Phone
            : $"ENC({request.Phone})";
        var addrEnc = request.Address.StartsWith("ENC(", StringComparison.Ordinal)
            ? request.Address
            : $"ENC({request.Address})";
        var emgEnc = request.EmergencyContact.StartsWith("ENC(", StringComparison.Ordinal)
            ? request.EmergencyContact
            : $"ENC({request.EmergencyContact})";

        driver.UpdateRates(
            new Money(request.HourlyRateAmount, request.HourlyRateCurrency),
            new Money(request.PerTripRateAmount, request.PerTripRateCurrency),
            new Money(request.PerKmRateAmount, request.PerKmRateCurrency));

        driver.UpdateLicence(request.LicenceClass, request.LicenceExpiry);
        driver.UpdateEncryptedContactInfo(phoneEnc, addrEnc, emgEnc);
        driver.SetStatus(request.Status);

        driverRepository.UpdateDriver(driver);

        if (user is not null)
        {
            user.UpdateProfile(request.DisplayName, user.AvatarKey, user.Locale);
            if (request.Status == DriverStatus.Inactive)
            {
                user.SetStatus(UserStatus.Inactive);
            }
            else if (request.Status == DriverStatus.Active && user.Status == UserStatus.Inactive)
            {
                user.SetStatus(UserStatus.Active);
            }
            driverRepository.UpdateUser(user);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
