using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Notifications.Fixtures;

namespace Nimpression.Integration.Tests.Notifications;

[Collection("PostgreSqlCollection")]
public sealed class F11_5_EmailLogInspectionAndManualResendIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly MailpitTestClient _mailpit = new();
    private NotificationTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private string _adminToken = string.Empty;

    public F11_5_EmailLogInspectionAndManualResendIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var adminEmail = TestDataFactory.CreateEmailAddress("admin");

        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();

            var admin = new User(_adminUserId, adminEmail, "HashPass123!", UserRole.Admin, "Admin");
            await db.Users.AddAsync(admin);
            await db.SaveChangesAsync();
        }

        await _mailpit.ClearAllMessagesAsync();
        _factory = new NotificationTestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (_adminToken, _) = jwtGenerator.GenerateAccessToken(_adminUserId, adminEmail.Value, UserRole.Admin.ToString(), "Admin");
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
        _mailpit.Dispose();
    }

    [Fact]
    public async Task EmailLogs_QueryAndManualResend_WorksSuccessfully()
    {
        // ── 1. 在数据库中插入一条发送失败的 EmailLog ──
        var logId = Guid.NewGuid();
        var toEmail = TestDataFactory.CreateEmailAddress("resend_target");
        var correlationId = $"CORR-RESEND-{Guid.NewGuid():N}";

        using (var db = _fixture.CreateDbContext())
        {
            var failedLog = new EmailLog(
                logId,
                "INCIDENT_NOTIFICATION",
                toEmail,
                "Incident Notification - Moderate - TEST",
                "IncidentService",
                correlationId);

            failedLog.RecordFailure("SMTP Service Unavailable");
            await db.EmailLogs.AddAsync(failedLog);
            await db.SaveChangesAsync();
        }

        // ── 2. 管理端分页查询日志并包含状态与错误详情 ──
        var queryResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/notifications/logs?status=Failed&searchTerm=Incident");
        queryResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await queryResp.Content.ReadFromJsonAsync<PagedResult<EmailLogDto>>();
        pagedResult.Should().NotBeNull();
        pagedResult!.Items.Should().Contain(l => l.Id == logId && l.Status == "Failed" && l.LastError == "SMTP Service Unavailable");

        // ── 3. 手动触发重发 ──
        var resendResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/notifications/logs/{logId}/resend");
        resendResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // ── 4. 再次查询确认状态已变更为 Sent ──
        var getLogResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/notifications/logs/{logId}");
        getLogResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedLog = await getLogResp.Content.ReadFromJsonAsync<EmailLogDto>();
        updatedLog!.Status.Should().Be("Sent");
        updatedLog.SentAt.Should().NotBeNull();

        // ── 5. 对已成功的邮件再次重发应返回 422 业务报错 ──
        var duplicateResendResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/notifications/logs/{logId}/resend");
        duplicateResendResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
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
