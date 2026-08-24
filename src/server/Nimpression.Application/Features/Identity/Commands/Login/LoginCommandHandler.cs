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
    /// <summary>
    /// 预置的固定 BCrypt workFactor=12 假哈希。
    /// 当用户不存在或邮箱格式不合法时，依然对该假哈希执行密码验证，将两类路径的耗时均拉平至 ~250ms，
    /// 彻底消除基于响应时间（Timing Oracle）枚举用户有效邮箱的时序侧信道漏洞。
    /// </summary>
    private const string DummyBcryptHash = "$2a$12$XZCHWVyJw9OQb10ZeqYcyeOQcZJ6bY5xH7Wl.c6kR4V1mQZ4m1aCe";

    public async Task<Result<LoginResultDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        EmailAddress? email = null;
        try
        {
            email = new EmailAddress(request.Email.Trim().ToLowerInvariant());
        }
        catch
        {
            // 格式不合法时不立即返回，依然执行假哈希校验拉平耗时
        }

        var user = email.HasValue
            ? await identityRepository.GetUserByEmailAsync(email.Value, cancellationToken)
            : null;

        if (user is null)
        {
            // 时序侧信道对齐：用户不存在时执行假 BCrypt 验证，使耗时与存在用户一致
            passwordHasher.VerifyPassword(request.Password, DummyBcryptHash);
            return Error.Unauthorized("AUTH_INVALID_CREDENTIALS", "Invalid email or password.");
        }

        // 先执行密码哈希比对：
        // 理由：如果在密码校验前直接返回 AUTH_LOCKED_OUT，攻击者可以通过错误码枚举已锁定的账号。
        // 改为先校验密码：
        // 1. 盲猜密码的攻击者由于密码错误，始终得到 AUTH_INVALID_CREDENTIALS，无法探测账号是否存在或锁定；
        // 2. 只有输入正确密码的真实用户，在命中锁定规则时才会收到 AUTH_LOCKED_OUT 明确提示。
        var isPasswordValid = passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            if (!user.IsLockedOut(now))
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
            }

            return Error.Unauthorized("AUTH_INVALID_CREDENTIALS", "Invalid email or password.");
        }

        if (user.IsLockedOut(now))
        {
            return Error.Unauthorized("AUTH_LOCKED_OUT", "Account is temporarily locked. Please try again later.");
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
