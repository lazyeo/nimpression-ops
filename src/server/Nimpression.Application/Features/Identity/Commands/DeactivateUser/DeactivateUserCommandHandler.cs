using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Identity.Commands.DeactivateUser;

public sealed class DeactivateUserCommandHandler(
    IIdentityRepository identityRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<DeactivateUserCommand, Result>
{
    public async Task<Result> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await identityRepository.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Error.NotFound("USER_NOT_FOUND", $"User '{request.UserId}' was not found.");
        }

        var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        user.SetStatus(UserStatus.Inactive);

        var activeTokens = await identityRepository.GetActiveRefreshTokensByUserIdAsync(user.Id, cancellationToken);
        foreach (var token in activeTokens)
        {
            token.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
