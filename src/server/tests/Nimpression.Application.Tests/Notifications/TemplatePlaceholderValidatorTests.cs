using FluentAssertions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Common;

namespace Nimpression.Application.Tests.Notifications;

public class TemplatePlaceholderValidatorTests
{
    [Theory]
    [InlineData("SERVICE_DUE_REMINDER", "Reminder", "提醒", "Due at {{CurrentOdometer}} km", "需在 {{CurrentOdometer}} km 保养", "VehicleRego")]
    [InlineData("SERVICE_DUE_REMINDER", "Reminder {{VehicleRego}}", "提醒 {{VehicleRego}}", "Due", "保养", "CurrentOdometer")]
    [InlineData("COMPLIANCE_EXPIRY_WARNING", "Warning {{ExpiryType}}", "预警 {{ExpiryType}}", "Due on {{ExpiryDate}}", "于 {{ExpiryDate}} 到期", "VehicleRego")]
    [InlineData("INCIDENT_NOTIFICATION", "Incident {{VehicleRego}}", "事故 {{VehicleRego}}", "Details", "详情", "Severity")]
    [InlineData("FINE_ACCEPTED_NOTICE", "Notice", "通知", "Fine paid", "罚单已付", "FineRef")]
    public void ValidateRequiredPlaceholders_WhenMissingRequiredPlaceholder_ReturnsUnprocessableError(
        string key, string subjectEn, string subjectZh, string bodyEn, string bodyZh, string missingPlaceholder)
    {
        // Act
        var result = TemplatePlaceholderValidator.ValidateRequiredPlaceholders(key, subjectEn, subjectZh, bodyEn, bodyZh);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("missing_template_placeholders");
        result.Error.Message.Should().Contain(missingPlaceholder);
    }

    [Fact]
    public void ValidateRequiredPlaceholders_WhenAllPlaceholdersPresent_ReturnsSuccess()
    {
        // Arrange
        const string key = "SERVICE_DUE_REMINDER";
        const string subjectEn = "Vehicle {{VehicleRego}} service due";
        const string subjectZh = "车辆 {{VehicleRego}} 保养提醒";
        const string bodyEn = "Vehicle {{VehicleRego}} reached {{CurrentOdometer}} km.";
        const string bodyZh = "车辆 {{VehicleRego}} 当前里程 {{CurrentOdometer}} 公里。";

        // Act
        var result = TemplatePlaceholderValidator.ValidateRequiredPlaceholders(key, subjectEn, subjectZh, bodyEn, bodyZh);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
