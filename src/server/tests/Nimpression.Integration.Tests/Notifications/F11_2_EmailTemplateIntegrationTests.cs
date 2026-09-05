using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Api.Endpoints;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Notifications.Common;
using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Notifications.Fixtures;

namespace Nimpression.Integration.Tests.Notifications;

[Collection("PostgreSqlCollection")]
public sealed class F11_2_EmailTemplateIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private NotificationTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _dispatcherUserId = Guid.NewGuid();
    private readonly Guid _driverUserId = Guid.NewGuid();
    private string _adminToken = string.Empty;
    private string _dispatcherToken = string.Empty;
    private string _driverToken = string.Empty;

    public F11_2_EmailTemplateIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var adminEmail = TestDataFactory.CreateEmailAddress("admin");
        var dispatcherEmail = TestDataFactory.CreateEmailAddress("dispatcher");
        var driverEmail = TestDataFactory.CreateEmailAddress("driver");

        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();

            var admin = new User(_adminUserId, adminEmail, "HashPass123!", UserRole.Admin, "Admin");
            var dispatcher = new User(_dispatcherUserId, dispatcherEmail, "HashPass123!", UserRole.Dispatcher, "Dispatcher");
            var driver = new User(_driverUserId, driverEmail, "HashPass123!", UserRole.Driver, "Driver");

            await db.Users.AddAsync(admin);
            await db.Users.AddAsync(dispatcher);
            await db.Users.AddAsync(driver);
            await db.SaveChangesAsync();
        }

        _factory = new NotificationTestWebApplicationFactory(_fixture);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (_adminToken, _) = jwtGenerator.GenerateAccessToken(_adminUserId, adminEmail.Value, UserRole.Admin.ToString(), "Admin");
        (_dispatcherToken, _) = jwtGenerator.GenerateAccessToken(_dispatcherUserId, dispatcherEmail.Value, UserRole.Dispatcher.ToString(), "Dispatcher");
        (_driverToken, _) = jwtGenerator.GenerateAccessToken(_driverUserId, driverEmail.Value, UserRole.Driver.ToString(), "Driver");
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task CreateTemplate_WhenMissingRequiredPlaceholder_Returns422UnprocessableEntity()
    {
        // Arrange: key SERVICE_DUE_REMINDER requires {{VehicleRego}} and {{CurrentOdometer}}
        // Clear any existing template for clean test
        await using (var db = _fixture.CreateDbContext())
        {
            var existing = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Key == NotificationTemplateKeys.ServiceDueReminder);
            if (existing is not null)
            {
                db.EmailTemplates.Remove(existing);
                await db.SaveChangesAsync();
            }
        }

        var req = new CreateEmailTemplateRequest(
            NotificationTemplateKeys.ServiceDueReminder,
            "Service Due Warning",
            "保养预警",
            "Please service vehicle",
            "请安排车辆保养",
            true);

        // Act
        var resp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/notifications/templates", req);

        // Assert: F11.2 - 缺失占位符在保存时校验报错 (422)
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateTemplate_WithAllRequiredPlaceholders_CreatesSuccessfully()
    {
        // Arrange
        var customKey = $"CUSTOM_SERVICE_{Guid.NewGuid():N}";
        var req = new CreateEmailTemplateRequest(
            customKey,
            "Service {{VehicleRego}}",
            "保养 {{VehicleRego}}",
            "Vehicle {{VehicleRego}} at {{CurrentOdometer}} km",
            "车辆 {{VehicleRego}} 里程 {{CurrentOdometer}} 公里",
            true);

        // Act
        var createResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/notifications/templates", req);

        // Assert
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var templateId = await createResp.Content.ReadFromJsonAsync<Guid>();
        templateId.Should().NotBeEmpty();

        // Query by key
        var getByKeyResp = await SendAuthorizedAsync(_dispatcherToken, HttpMethod.Get, $"/api/notifications/templates/by-key/{customKey.ToUpperInvariant()}");
        getByKeyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await getByKeyResp.Content.ReadFromJsonAsync<EmailTemplateDto>();
        dto.Should().NotBeNull();
        dto!.Key.Should().Be(customKey.ToUpperInvariant());
        dto.SubjectEn.Should().Be("Service {{VehicleRego}}");
    }

    [Fact]
    public async Task UpdateTemplate_WhenMissingPlaceholder_Returns422UnprocessableEntity()
    {
        // 1. Clear existing INCIDENT_NOTIFICATION if present
        await using (var db = _fixture.CreateDbContext())
        {
            var existing = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Key == NotificationTemplateKeys.IncidentNotification);
            if (existing is not null)
            {
                db.EmailTemplates.Remove(existing);
                await db.SaveChangesAsync();
            }
        }

        // 2. Create valid template
        var createReq = new CreateEmailTemplateRequest(
            NotificationTemplateKeys.IncidentNotification,
            "Incident {{Severity}} - {{VehicleRego}}",
            "事故 {{Severity}} - {{VehicleRego}}",
            "Reported for {{VehicleRego}} with severity {{Severity}}",
            "车辆 {{VehicleRego}} 发生事故，严重度 {{Severity}}",
            true);

        var createResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/notifications/templates", createReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var templateId = await createResp.Content.ReadFromJsonAsync<Guid>();

        // 3. Try update missing {{VehicleRego}}
        var updateReq = new UpdateEmailTemplateRequest(
            "Incident {{Severity}}",
            "事故 {{Severity}}",
            "Reported with severity {{Severity}}",
            "发生事故，严重度 {{Severity}}");

        var updateResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Put, $"/api/notifications/templates/{templateId}", updateReq);

        // Assert
        updateResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task TemplateMutations_WhenDispatcherAttemptsWrite_ReturnsForbidden()
    {
        var customKey = $"FORBIDDEN_{Guid.NewGuid():N}";
        var req = new CreateEmailTemplateRequest(
            customKey,
            "Sub", "主题", "Body", "内容", true);

        var resp = await SendAuthorizedAsync(_dispatcherToken, HttpMethod.Post, "/api/notifications/templates", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(string token, HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }
}
