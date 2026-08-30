using FluentAssertions;
using Nimpression.Application.Features.Privacy.Queries.GetDataClassification;
using Xunit;

namespace Nimpression.Application.Tests.Privacy.Queries;

public sealed class GetDataClassificationQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_complete_data_classification_catalog()
    {
        var sut = new GetDataClassificationQueryHandler();
        var query = new GetDataClassificationQuery();

        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var catalog = result.Value;
        catalog.Should().NotBeEmpty();

        // 验证关键 PII 字段均已包含并正确标记
        catalog.Should().Contain(c => c.EntityName == "Driver" && c.FieldName == "PhoneEnc" && c.IsEncryptedAtRest && c.SensitivityLevel.Contains("PII"));
        catalog.Should().Contain(c => c.EntityName == "Driver" && c.FieldName == "AddressEnc" && c.IsEncryptedAtRest);
        catalog.Should().Contain(c => c.EntityName == "Driver" && c.FieldName == "EmergencyContactEnc" && c.IsEncryptedAtRest);
        catalog.Should().Contain(c => c.EntityName == "Vehicle" && c.FieldName == "VinEnc" && c.IsEncryptedAtRest);
        catalog.Should().Contain(c => c.EntityName == "IncidentReport" && c.FieldName == "ThirdPartyInfoEnc" && c.IsEncryptedAtRest);
        catalog.Should().Contain(c => c.EntityName == "ShiftEntry" && c.FieldName.Contains("ClockInLat") && c.RetentionPeriod.Contains("90 days"));
        catalog.Should().Contain(c => c.EntityName == "Payslip" && c.RetentionPeriod.Contains("7 years"));
    }
}
