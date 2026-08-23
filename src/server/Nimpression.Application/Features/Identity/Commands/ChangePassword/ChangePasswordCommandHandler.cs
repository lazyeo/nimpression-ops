using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Identity.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler(
    IIdentityRepository identityRepository,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        // 越权防护（N1.3）：非管理员用户只能修改本人密码
        if (currentUser.Role != UserRole.Admin && currentUser.UserId != request.UserId)
        {
            return Error.Forbidden("AUTH_FORBIDDEN", "Forbidden: Cannot change password of another user.");
        }

        var user = await identityRepository.GetUserByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Error.NotFound("USER_NOT_FOUND", $"User '{request.UserId}' was not found.");
        }

        if (!passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return Error.Unauthorized("AUTH_INVALID_CREDENTIALS", "Current password is incorrect.");
        }

        var newHash = passwordHasher.HashPassword(request.NewPassword);
        user.SetPasswordHash(newHash);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
