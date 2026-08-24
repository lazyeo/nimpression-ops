using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Identity.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    IIdentityRepository identityRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IUnitOfWork unitOfWork,
    IAuditSink auditSink,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<RefreshTokenCommand, Result<LoginResultDto>>
{
    public async Task<Result<LoginResultDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken))
        {
            return Error.Unauthorized("AUTH_TOKEN_MISSING", "Refresh token is missing.");
        }

        var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        var tokenHash = jwtTokenGenerator.HashRefreshToken(request.RawRefreshToken);

        var token = await identityRepository.GetRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (token is null)
        {
            return Error.Unauthorized("AUTH_TOKEN_INVALID", "Invalid refresh token.");
        }

        if (token.IsRevoked)
        {
            // 防重放攻击拦截：旧令牌已被轮转或撤销，一旦被重复提交，立即撤销该用户的所有活跃刷新令牌并记录审计
            var activeTokens = await identityRepository.GetActiveRefreshTokensByUserIdAsync(token.UserId, cancellationToken);
            foreach (var activeToken in activeTokens)
            {
                activeToken.Revoke(now);
            }

            await auditSink.RecordAsync(
                entityType: "User",
                entityId: token.UserId,
                action: "Security.RefreshTokenReplayDetected",
                beforeJson: null,
                afterJson: $"{{\"replayedTokenId\":\"{token.Id}\",\"revokedTokensCount\":{activeTokens.Count},\"ipAddress\":\"{request.IpAddress}\"}}",
                cancellationToken: cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Error.Unauthorized("AUTH_TOKEN_REVOKED", "Refresh token has been revoked.");
        }

        if (token.IsExpired(now))
        {
            return Error.Unauthorized("AUTH_TOKEN_EXPIRED", "Refresh token has expired.");
        }

        var user = await identityRepository.GetUserByIdAsync(token.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            token.Revoke(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Error.Unauthorized("AUTH_ACCOUNT_INACTIVE", "Account is inactive.");
        }

        var (accessToken, expiresIn) = jwtTokenGenerator.GenerateAccessToken(
            user.Id,
            user.Email.Value,
            user.Role.ToString(),
            user.DisplayName);

        var (newRawRefreshToken, newTokenHash, newRefreshExpiresAt) = jwtTokenGenerator.GenerateRefreshToken(request.IpAddress);

        var newRefreshTokenId = Guid.NewGuid();
        token.Revoke(now, replacedById: newRefreshTokenId);

        var newRefreshToken = new Domain.Entities.Identity.RefreshToken(
            newRefreshTokenId,
            user.Id,
            newTokenHash,
            newRefreshExpiresAt,
            request.IpAddress,
            now);

        await identityRepository.AddRefreshTokenAsync(newRefreshToken, cancellationToken);
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
            newRawRefreshToken,
            newRefreshExpiresAt,
            authUser);
    }
}
