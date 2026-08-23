using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Identity.Commands.Login;

public sealed class LoginCommandHandler(
    IIdentityRepository identityRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IUnitOfWork unitOfWork,
    IAuditSink auditSink,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<LoginCommand, Result<LoginResultDto>>
{
    public async Task<Result<LoginResultDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        EmailAddress email;
        try
        {
            email = new EmailAddress(request.Email.Trim().ToLowerInvariant());
        }
        catch
        {
            return Error.Unauthorized("AUTH_INVALID_CREDENTIALS", "Invalid email or password.");
        }

        var user = await identityRepository.GetUserByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized("AUTH_INVALID_CREDENTIALS", "Invalid email or password.");
        }

        if (user.IsLockedOut(now))
        {
            return Error.Unauthorized("AUTH_LOCKED_OUT", "Account is temporarily locked. Please try again later.");
        }

        var isPasswordValid = passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            user.RecordLoginFailure(now, maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

            if (user.IsLockedOut(now))
            {
                await auditSink.RecordAsync(
                    entityType: "User",
                    entityId: user.Id,
                    action: "User.Lockout",
                    beforeJson: null,
                    afterJson: $"{{\"reason\":\"5 consecutive failed login attempts\",\"lockoutEnd\":\"{user.LockoutEnd:O}\"}}",
                    cancellationToken: cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Error.Unauthorized("AUTH_INVALID_CREDENTIALS", "Invalid email or password.");
        }

        if (user.Status != UserStatus.Active)
        {
            return Error.Forbidden("AUTH_ACCOUNT_INACTIVE", "Account is deactivated.");
        }

        user.RecordLoginSuccess(now);

        var (accessToken, expiresIn) = jwtTokenGenerator.GenerateAccessToken(
            user.Id,
            user.Email.Value,
            user.Role.ToString(),
            user.DisplayName);

        var (rawRefreshToken, tokenHash, refreshExpiresAt) = jwtTokenGenerator.GenerateRefreshToken(request.IpAddress);

        var refreshToken = new Domain.Entities.Identity.RefreshToken(
            Guid.NewGuid(),
            user.Id,
            tokenHash,
            refreshExpiresAt,
            request.IpAddress,
            now);

        await identityRepository.AddRefreshTokenAsync(refreshToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var authUser = new AuthUserDto(
            user.Id,
            user.Email.Value,
            user.DisplayName,
            user.Role,
            user.Locale,
            user.AvatarKey);

        return new LoginResultDto(
            accessToken,
            expiresIn,
            "Bearer",
            rawRefreshToken,
            refreshExpiresAt,
            authUser);
    }
}
