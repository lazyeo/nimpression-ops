using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Api.Endpoints;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Notifications.Fixtures;

namespace Nimpression.Integration.Tests.Notifications;

[Collection("PostgreSqlCollection")]
public sealed class F11_1_PartnerContactIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private NotificationTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _driverUserId = Guid.NewGuid();
    private string _adminToken = string.Empty;
    private string _driverToken = string.Empty;

    public F11_1_PartnerContactIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var adminEmail = TestDataFactory.CreateEmailAddress("admin");
        var driverEmail = TestDataFactory.CreateEmailAddress("driver");

        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();

            var admin = new User(_adminUserId, adminEmail, "HashPass123!", UserRole.Admin, "Admin User");
            var driver = new User(_driverUserId, driverEmail, "HashPass123!", UserRole.Driver, "Driver User");

            await db.Users.AddAsync(admin);
            await db.Users.AddAsync(driver);
            await db.SaveChangesAsync();
        }

        _factory = new NotificationTestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (_adminToken, _) = jwtGenerator.GenerateAccessToken(_adminUserId, adminEmail.Value, UserRole.Admin.ToString(), "Admin User");
        (_driverToken, _) = jwtGenerator.GenerateAccessToken(_driverUserId, driverEmail.Value, UserRole.Driver.ToString(), "Driver User");
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
    public async Task PartnerContacts_FullLifecycleCrud_WorksSuccessfully()
    {
        // 1. Create Partner Contact (Admin)
        var companyName = $"Insurer_{Guid.NewGuid():N}";
        var email = TestDataFactory.CreateEmail("partner");

        var createReq = new CreatePartnerContactRequest(PartnerKind.Insurer, companyName, email, true);
        var createResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/notifications/partner-contacts", createReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdId = await createResp.Content.ReadFromJsonAsync<Guid>();
        createdId.Should().NotBeEmpty();

        // 2. Get by ID
        var getResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/notifications/partner-contacts/{createdId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await getResp.Content.ReadFromJsonAsync<PartnerContactDto>();
        dto.Should().NotBeNull();
        dto!.CompanyName.Should().Be(companyName);
        dto.Kind.Should().Be(PartnerKind.Insurer);
        dto.Email.Should().Be(email);
        dto.Active.Should().BeTrue();

        // 3. Update details
        var updatedName = $"Updated_{companyName}";
        var updatedEmail = TestDataFactory.CreateEmail("updated_partner");
        var updateReq = new UpdatePartnerContactRequest(PartnerKind.Maintenance, updatedName, updatedEmail);
        var updateResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Put, $"/api/notifications/partner-contacts/{createdId}", updateReq);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var getUpdatedResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/notifications/partner-contacts/{createdId}");
        var updatedDto = await getUpdatedResp.Content.ReadFromJsonAsync<PartnerContactDto>();
        updatedDto!.CompanyName.Should().Be(updatedName);
        updatedDto.Kind.Should().Be(PartnerKind.Maintenance);
        updatedDto.Email.Should().Be(updatedEmail);

        // 4. Deactivate contact
        var deactResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/notifications/partner-contacts/{createdId}/deactivate");
        deactResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var getDeactResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/notifications/partner-contacts/{createdId}");
        var deactDto = await getDeactResp.Content.ReadFromJsonAsync<PartnerContactDto>();
        deactDto!.Active.Should().BeFalse();

        // 5. Activate contact
        var actResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/notifications/partner-contacts/{createdId}/activate");
        actResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var getActResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/notifications/partner-contacts/{createdId}");
        var actDto = await getActResp.Content.ReadFromJsonAsync<PartnerContactDto>();
        actDto!.Active.Should().BeTrue();

        // 6. Delete contact
        var delResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Delete, $"/api/notifications/partner-contacts/{createdId}");
        delResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var getDeletedResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/notifications/partner-contacts/{createdId}");
        getDeletedResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PartnerContacts_WhenDriverAttemptsMutations_ReturnsForbidden()
    {
        var createReq = new CreatePartnerContactRequest(PartnerKind.Insurer, "Forbidden Corp", TestDataFactory.CreateEmail("driver_hack"), true);
        var resp = await SendAuthorizedAsync(_driverToken, HttpMethod.Post, "/api/notifications/partner-contacts", createReq);
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
