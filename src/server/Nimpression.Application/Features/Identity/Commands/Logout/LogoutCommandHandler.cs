using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.Abstractions;

namespace Nimpression.Application.Features.Identity.Commands.Logout;

public sealed class LogoutCommandHandler(
    IIdentityRepository identityRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IUnitOfWork unitOfWork,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken))
        {
            return Result.Success();
        }

        var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        var tokenHash = jwtTokenGenerator.HashRefreshToken(request.RawRefreshToken);

        var token = await identityRepository.GetRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (token is not null && !token.IsRevoked)
        {
            token.Revoke(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
