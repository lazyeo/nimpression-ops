using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Drivers.Commands.CreateDriver;

/// <summary>
/// 创建司机命令处理器。
/// </summary>
public sealed class CreateDriverCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher? passwordHasher = null,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<CreateDriverCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        EmailAddress email;
        try
        {
            email = new EmailAddress(request.Email);
        }
        catch (Exception ex)
        {
            return Error.Validation("invalid_email", ex.Message);
        }

        if (await driverRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            return Error.Conflict("email_conflict", $"User with email '{email.Value}' already exists.");
        }

        var normalizedEmployeeNo = request.EmployeeNo.Trim().ToUpperInvariant();
        if (await driverRepository.ExistsByEmployeeNoAsync(normalizedEmployeeNo, cancellationToken))
        {
            return Error.Conflict("employee_no_conflict", $"Driver with employee number '{normalizedEmployeeNo}' already exists.");
        }

        var userId = Guid.NewGuid();
        var driverId = request.Id ?? Guid.NewGuid();
        request.CreatedId = driverId;

        var rawPassword = string.IsNullOrWhiteSpace(request.Password) ? "dev-only-insecure-temp-password-123!" : request.Password;
        var passwordHash = passwordHasher?.HashPassword(rawPassword)
            ?? "$2a$12$e8Y6bFvU2.i/sD.y/5pMhuo1KzMh1k1R4k0A6W/o8L2m2o8g/4W8.";

        var hiredOn = request.HiredOn == default
            ? (dateTimeProvider?.NzToday ?? DateOnly.FromDateTime(DateTime.UtcNow))
            : request.HiredOn;

        var user = new User(
            userId,
            email,
            passwordHash,
            UserRole.Driver,
            request.DisplayName,
            "en-NZ",
            dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow);

        var phoneEnc = request.Phone.StartsWith("ENC(", StringComparison.Ordinal)
            ? request.Phone
            : $"ENC({request.Phone})";
        var addrEnc = request.Address.StartsWith("ENC(", StringComparison.Ordinal)
            ? request.Address
            : $"ENC({request.Address})";
        var emgEnc = request.EmergencyContact.StartsWith("ENC(", StringComparison.Ordinal)
            ? request.EmergencyContact
            : $"ENC({request.EmergencyContact})";

        var driver = new Driver(
            driverId,
            userId,
            normalizedEmployeeNo,
            request.LicenceClass,
            request.LicenceExpiry,
            new Money(request.HourlyRateAmount, request.HourlyRateCurrency),
            new Money(request.PerTripRateAmount, request.PerTripRateCurrency),
            new Money(request.PerKmRateAmount, request.PerKmRateCurrency),
            phoneEnc,
            addrEnc,
            emgEnc,
            hiredOn,
            DriverStatus.Active);

        List<AreaAssignment>? assignments = null;
        if (request.AreaIds is { Count: > 0 })
        {
            assignments = request.AreaIds
                .Distinct()
                .Select(areaId => new AreaAssignment(
                    Guid.NewGuid(),
                    areaId,
                    driverId,
                    hiredOn,
                    null))
                .ToList();
        }

        await driverRepository.AddDriverAsync(driver, user, assignments, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return driverId;
    }
}
