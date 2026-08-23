using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.Commands.UpdateDriverSelfProfile;

/// <summary>
/// 司机个人资料自助修改命令处理器（F2.4）。
/// </summary>
public sealed class UpdateDriverSelfProfileCommandHandler(
    IDriverRepository driverRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateDriverSelfProfileCommand, Result>
{
    public async Task<Result> Handle(
        UpdateDriverSelfProfileCommand request,
        CancellationToken cancellationToken)
    {
        // 关键约束：司机不可改工号、时薪/趟次/里程费率、状态与驾照（F2.4 明确要求返回 403）
        if (!string.IsNullOrWhiteSpace(request.AttemptedEmployeeNo) ||
            request.AttemptedHourlyRate.HasValue ||
            request.AttemptedPerTripRate.HasValue ||
            request.AttemptedPerKmRate.HasValue ||
            request.AttemptedStatus.HasValue ||
            request.AttemptedLicenceExpiry.HasValue)
        {
            return Error.Forbidden(
                "forbidden_field_modification",
                "Drivers are not permitted to modify employee number, wage rates, licence expiry, or status.");
        }

        var driver = await driverRepository.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId}' was not found.");
        }

        // 越权防护：司机只能改自己的资料（N1.3）
        if (currentUser.Role == UserRole.Driver && currentUser.UserId != driver.UserId)
        {
            return Error.Forbidden("forbidden", "Drivers can only update their own profile.");
        }

        var user = await driverRepository.GetUserByIdAsync(driver.UserId, cancellationToken);
        if (user is null)
        {
            return Error.NotFound("user_not_found", $"User with ID '{driver.UserId}' was not found.");
        }

        var phoneEnc = request.Phone.StartsWith("ENC(", StringComparison.Ordinal)
            ? request.Phone
            : $"ENC({request.Phone})";
        var emgEnc = request.EmergencyContact.StartsWith("ENC(", StringComparison.Ordinal)
            ? request.EmergencyContact
            : $"ENC({request.EmergencyContact})";
        var addrEnc = request.Address != null
            ? (request.Address.StartsWith("ENC(", StringComparison.Ordinal) ? request.Address : $"ENC({request.Address})")
            : driver.AddressEnc;

        driver.UpdateEncryptedContactInfo(phoneEnc, addrEnc, emgEnc);
        driverRepository.UpdateDriver(driver);

        user.UpdateProfile(user.DisplayName, user.AvatarKey, request.Locale);
        driverRepository.UpdateUser(user);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
