using FluentAssertions;
using NSubstitute;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.EmailTemplates.Commands.ActivateEmailTemplate;
using Nimpression.Application.Features.Notifications.EmailTemplates.Commands.CreateEmailTemplate;
using Nimpression.Application.Features.Notifications.EmailTemplates.Commands.DeactivateEmailTemplate;
using Nimpression.Application.Features.Notifications.EmailTemplates.Commands.UpdateEmailTemplate;
using Nimpression.Domain.Entities.Communications;

namespace Nimpression.Application.Tests.Notifications;

public class EmailTemplateCommandsTests
{
    private readonly IEmailTemplateRepository _templateRepo = Substitute.For<IEmailTemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task CreateEmailTemplate_WhenMissingPlaceholder_Returns422Unprocessable()
    {
        // Arrange: missing {{VehicleRego}} for SERVICE_DUE_REMINDER
        var handler = new CreateEmailTemplateCommandHandler(_templateRepo, _unitOfWork);
        var command = new CreateEmailTemplateCommand(
            "SERVICE_DUE_REMINDER",
            "Service Due",
            "保养提醒",
            "Due at {{CurrentOdometer}} km",
            "需在 {{CurrentOdometer}} km 保养",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("missing_template_placeholders");
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<EmailTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateEmailTemplate_WithAllPlaceholders_Succeeds()
    {
        // Arrange
        var handler = new CreateEmailTemplateCommandHandler(_templateRepo, _unitOfWork);
        var command = new CreateEmailTemplateCommand(
            "SERVICE_DUE_REMINDER",
            "Service Due {{VehicleRego}}",
            "车辆 {{VehicleRego}} 保养提醒",
            "Due at {{CurrentOdometer}} km",
            "需在 {{CurrentOdometer}} km 保养",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _templateRepo.Received(1).AddAsync(Arg.Any<EmailTemplate>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateEmailTemplate_WhenMissingPlaceholder_Returns422Unprocessable()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new EmailTemplate(
            templateId,
            "FINE_ACCEPTED_NOTICE",
            "Notice {{FineRef}}",
            "通知 {{FineRef}}",
            "Fine {{FineRef}} accepted",
            "罚单 {{FineRef}} 已确认",
            true);

        _templateRepo.GetByIdAsync(templateId, Arg.Any<CancellationToken>()).Returns(template);

        var handler = new UpdateEmailTemplateCommandHandler(_templateRepo, _unitOfWork);
        // Missing {{FineRef}} in update
        var command = new UpdateEmailTemplateCommand(
            templateId,
            "Notice without ref",
            "无编号通知",
            "Accepted",
            "已确认");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("missing_template_placeholders");
    }

    [Fact]
    public async Task ActivateAndDeactivateEmailTemplate_TogglesActiveState()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new EmailTemplate(
            templateId,
            "FINE_ACCEPTED_NOTICE",
            "Notice {{FineRef}}",
            "通知 {{FineRef}}",
            "Fine {{FineRef}} accepted",
            "罚单 {{FineRef}} 已确认",
            true);

        _templateRepo.GetByIdAsync(templateId, Arg.Any<CancellationToken>()).Returns(template);

        var deactHandler = new DeactivateEmailTemplateCommandHandler(_templateRepo, _unitOfWork);
        var actHandler = new ActivateEmailTemplateCommandHandler(_templateRepo, _unitOfWork);

        // Act 1: Deactivate
        var deactResult = await deactHandler.Handle(new DeactivateEmailTemplateCommand(templateId), CancellationToken.None);
        deactResult.IsSuccess.Should().BeTrue();
        template.Active.Should().BeFalse();

        // Act 2: Activate
        var actResult = await actHandler.Handle(new ActivateEmailTemplateCommand(templateId), CancellationToken.None);
        actResult.IsSuccess.Should().BeTrue();
        template.Active.Should().BeTrue();
    }
}
