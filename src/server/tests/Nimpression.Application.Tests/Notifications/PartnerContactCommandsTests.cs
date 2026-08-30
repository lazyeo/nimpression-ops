using FluentAssertions;
using NSubstitute;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.ActivatePartnerContact;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.CreatePartnerContact;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.DeactivatePartnerContact;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.DeletePartnerContact;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.UpdatePartnerContact;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Tests.Notifications;

public class PartnerContactCommandsTests
{
    private readonly IPartnerContactRepository _partnerRepo = Substitute.For<IPartnerContactRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task CreatePartnerContact_WithValidData_ReturnsSuccessAndId()
    {
        // Arrange
        var handler = new CreatePartnerContactCommandHandler(_partnerRepo, _unitOfWork);
        var command = new CreatePartnerContactCommand(PartnerKind.Insurer, "AA Insurance", "claims@aainsurance.co.nz", true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _partnerRepo.Received(1).AddAsync(Arg.Any<PartnerContact>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartnerContact_WithInvalidEmail_ReturnsValidationError()
    {
        // Arrange
        var handler = new CreatePartnerContactCommandHandler(_partnerRepo, _unitOfWork);
        var command = new CreatePartnerContactCommand(PartnerKind.Insurer, "AA Insurance", "invalid-email-no-at", true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task UpdatePartnerContact_WhenFound_UpdatesAndReturnsSuccess()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var existing = new PartnerContact(contactId, PartnerKind.Insurer, "Old Name", new EmailAddress("old@insurer.co.nz"), true);
        _partnerRepo.GetByIdAsync(contactId, Arg.Any<CancellationToken>()).Returns(existing);

        var handler = new UpdatePartnerContactCommandHandler(_partnerRepo, _unitOfWork);
        var command = new UpdatePartnerContactCommand(contactId, PartnerKind.Maintenance, "New Name", "new@maint.co.nz");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existing.CompanyName.Should().Be("New Name");
        existing.Kind.Should().Be(PartnerKind.Maintenance);
        existing.Email.Value.Should().Be("new@maint.co.nz");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartnerContact_WhenNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        _partnerRepo.GetByIdAsync(contactId, Arg.Any<CancellationToken>()).Returns((PartnerContact?)null);

        var handler = new UpdatePartnerContactCommandHandler(_partnerRepo, _unitOfWork);
        var command = new UpdatePartnerContactCommand(contactId, PartnerKind.Maintenance, "New Name", "new@maint.co.nz");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task DeactivateAndActivatePartnerContact_ChangesActiveStatus()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var contact = new PartnerContact(contactId, PartnerKind.Inspection, "VTNZ", new EmailAddress("vtnz@inspection.co.nz"), true);
        _partnerRepo.GetByIdAsync(contactId, Arg.Any<CancellationToken>()).Returns(contact);

        var deactivateHandler = new DeactivatePartnerContactCommandHandler(_partnerRepo, _unitOfWork);
        var activateHandler = new ActivatePartnerContactCommandHandler(_partnerRepo, _unitOfWork);

        // Act 1: Deactivate
        var deactResult = await deactivateHandler.Handle(new DeactivatePartnerContactCommand(contactId), CancellationToken.None);
        deactResult.IsSuccess.Should().BeTrue();
        contact.Active.Should().BeFalse();

        // Act 2: Activate
        var actResult = await activateHandler.Handle(new ActivatePartnerContactCommand(contactId), CancellationToken.None);
        actResult.IsSuccess.Should().BeTrue();
        contact.Active.Should().BeTrue();
    }

    [Fact]
    public async Task DeletePartnerContact_WhenFound_RemovesAndReturnsSuccess()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var contact = new PartnerContact(contactId, PartnerKind.Inspection, "VTNZ", new EmailAddress("vtnz@inspection.co.nz"), true);
        _partnerRepo.GetByIdAsync(contactId, Arg.Any<CancellationToken>()).Returns(contact);

        var handler = new DeletePartnerContactCommandHandler(_partnerRepo, _unitOfWork);

        // Act
        var result = await handler.Handle(new DeletePartnerContactCommand(contactId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _partnerRepo.Received(1).Remove(contact);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
