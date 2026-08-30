using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Common;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Notifications.PartnerContacts.Commands.CreatePartnerContact;

/// <summary>
/// 创建外部伙伴联系人命令（F11.1）。
/// </summary>
public sealed record CreatePartnerContactCommand(
    PartnerKind Kind,
    string CompanyName,
    string Email,
    bool Active = true) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "PartnerContact.Created";
    public string AuditEntityType => "PartnerContact";
    public Guid? AuditEntityId => null;
}

public sealed class CreatePartnerContactCommandValidator : AbstractValidator<CreatePartnerContactCommand>
{
    public CreatePartnerContactCommandValidator()
    {
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

public sealed class CreatePartnerContactCommandHandler(
    IPartnerContactRepository partnerContactRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreatePartnerContactCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePartnerContactCommand request, CancellationToken cancellationToken)
    {
        PartnerContact partnerContact;
        try
        {
            var emailAddress = new EmailAddress(request.Email);
            partnerContact = new PartnerContact(
                Guid.NewGuid(),
                request.Kind,
                request.CompanyName,
                emailAddress,
                request.Active);
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_partner_data", ex.Message);
        }

        try
        {
            await partnerContactRepository.AddAsync(partnerContact, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            return Error.Conflict("partner_contact_conflict", $"Partner contact '{request.CompanyName}' already exists.");
        }

        return partnerContact.Id;
    }
}
