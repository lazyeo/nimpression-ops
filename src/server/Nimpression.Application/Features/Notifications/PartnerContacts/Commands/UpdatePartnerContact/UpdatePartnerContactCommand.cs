using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Common;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Notifications.PartnerContacts.Commands.UpdatePartnerContact;

/// <summary>
/// 更新外部伙伴联系人信息命令（F11.1）。
/// </summary>
public sealed record UpdatePartnerContactCommand(
    Guid Id,
    PartnerKind Kind,
    string CompanyName,
    string Email) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "PartnerContact.Updated";
    public string AuditEntityType => "PartnerContact";
    public Guid? AuditEntityId => Id;
}

public sealed class UpdatePartnerContactCommandValidator : AbstractValidator<UpdatePartnerContactCommand>
{
    public UpdatePartnerContactCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Partner contact ID is required.");

        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage("Valid partner kind must be specified.");

        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .WithMessage("Company name cannot be empty.")
            .MaximumLength(150)
            .WithMessage("Company name cannot exceed 150 characters.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email cannot be empty.")
            .EmailAddress()
            .WithMessage("Invalid email format.")
            .MaximumLength(254)
            .WithMessage("Email cannot exceed 254 characters.");
    }
}

public sealed class UpdatePartnerContactCommandHandler(
    IPartnerContactRepository partnerContactRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdatePartnerContactCommand, Result>
{
    public async Task<Result> Handle(UpdatePartnerContactCommand request, CancellationToken cancellationToken)
    {
        var partnerContact = await partnerContactRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partnerContact is null)
        {
            return Error.NotFound("partner_contact_not_found", $"Partner contact with ID '{request.Id}' was not found.");
        }

        try
        {
            partnerContact.UpdateDetails(request.Kind, request.CompanyName, new EmailAddress(request.Email));
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_partner_data", ex.Message);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            return Error.Conflict("partner_contact_conflict", $"Partner contact '{request.CompanyName}' already exists.");
        }

        return Result.Success();
    }
}
