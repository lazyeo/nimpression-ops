using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Privacy.Queries.GetDataSubjectRequests;

public sealed record GetDataSubjectRequestsQuery(Guid? UserId = null) : IRequest<Result<IReadOnlyList<DataSubjectRequestDto>>>;

public sealed class GetDataSubjectRequestsQueryHandler(
    IPrivacyRepository privacyRepository,
    ICurrentUser currentUser) : IRequestHandler<GetDataSubjectRequestsQuery, Result<IReadOnlyList<DataSubjectRequestDto>>>
{
    public async Task<Result<IReadOnlyList<DataSubjectRequestDto>>> Handle(
        GetDataSubjectRequestsQuery request,
        CancellationToken cancellationToken)
    {
        // 司机只能查看自己的 DSR 请求
        if (currentUser.Role == UserRole.Driver)
        {
            var userDsr = await privacyRepository.GetDataSubjectRequestsAsync(currentUser.UserId, cancellationToken);
            return Result<IReadOnlyList<DataSubjectRequestDto>>.Success(userDsr);
        }

        var list = await privacyRepository.GetDataSubjectRequestsAsync(request.UserId, cancellationToken);
        return Result<IReadOnlyList<DataSubjectRequestDto>>.Success(list);
    }
}
