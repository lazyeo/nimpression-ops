using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;

namespace Nimpression.Application.Features.Privacy.Queries.GetPrivacyConsentStatus;

public sealed record GetPrivacyConsentStatusQuery(string PolicyVersion = "2026.1") : IRequest<Result<PrivacyConsentDto>>;

public sealed class GetPrivacyConsentStatusQueryHandler(
    IPrivacyRepository privacyRepository,
    ICurrentUser currentUser) : IRequestHandler<GetPrivacyConsentStatusQuery, Result<PrivacyConsentDto>>
{
    public async Task<Result<PrivacyConsentDto>> Handle(
        GetPrivacyConsentStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        var status = await privacyRepository.GetPrivacyConsentStatusAsync(
            currentUser.UserId.Value,
            request.PolicyVersion,
            cancellationToken);

        return Result<PrivacyConsentDto>.Success(status);
    }
}
