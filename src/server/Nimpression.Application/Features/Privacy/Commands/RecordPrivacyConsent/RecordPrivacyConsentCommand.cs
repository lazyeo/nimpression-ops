using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Privacy.Abstractions;
using Nimpression.Application.Features.Privacy.DTOs;

namespace Nimpression.Application.Features.Privacy.Commands.RecordPrivacyConsent;

/// <summary>
/// 记录用户隐私声明同意记录（AC N2.7）。
/// </summary>
public sealed record RecordPrivacyConsentCommand(
    string PolicyVersion = "2026.1") : IRequest<Result<PrivacyConsentDto>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "PrivacyConsent";
    public Guid? AuditEntityId => null;
    public string AuditAction => "RecordPrivacyConsent";
}

public sealed class RecordPrivacyConsentCommandValidator : AbstractValidator<RecordPrivacyConsentCommand>
{
    public RecordPrivacyConsentCommandValidator()
    {
        RuleFor(x => x.PolicyVersion)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("PolicyVersion is required and must not exceed 50 characters.");
    }
}

public sealed class RecordPrivacyConsentCommandHandler(
    IPrivacyRepository privacyRepository,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<RecordPrivacyConsentCommand, Result<PrivacyConsentDto>>
{
    public async Task<Result<PrivacyConsentDto>> Handle(
        RecordPrivacyConsentCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        var now = dateTimeProvider.UtcNow;
        await privacyRepository.RecordPrivacyConsentAsync(
            currentUser.UserId.Value,
            request.PolicyVersion,
            now,
            currentUser.IpAddress,
            currentUser.UserAgent,
            cancellationToken);

        var status = await privacyRepository.GetPrivacyConsentStatusAsync(
            currentUser.UserId.Value,
            request.PolicyVersion,
            cancellationToken);

        return Result<PrivacyConsentDto>.Success(status);
    }
}
