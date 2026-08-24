using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Identity.Queries.GetUserById;

public sealed class GetUserByIdQueryHandler(
    IIdentityRepository identityRepository,
    ICurrentUser currentUser,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        // 越权防护（N1.3）：司机角色只能访问本人的资源，访问他人资源返回 403 Forbidden
        if (currentUser.Role == UserRole.Driver && currentUser.UserId != request.Id)
        {
            return Error.Forbidden("AUTH_FORBIDDEN", "Forbidden: Cannot access another user's profile.");
        }

        var user = await identityRepository.GetUserByIdAsync(request.Id, cancellationToken);
        if (user is null)
        {
            return Error.NotFound("USER_NOT_FOUND", $"User '{request.Id}' was not found.");
        }

        var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        var dto = new UserDto(
            user.Id,
            user.Email.Value,
            user.DisplayName,
            user.Role,
            user.Status,
            user.Locale,
            user.AvatarKey,
            user.CreatedAt,
            user.LastLoginAt,
            user.IsLockedOut(now));

        return dto;
    }
}
