using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Api.Endpoints;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Incidents.DTOs;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fines;
using Nimpression.Integration.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace Nimpression.Integration.Tests.Incidents;

[Collection("PostgreSqlCollection")]
public sealed class IncidentIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private ComplianceTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _driverAUserId = Guid.NewGuid();
    private readonly Guid _driverADriverId = Guid.NewGuid();
    private readonly Guid _driverBUserId = Guid.NewGuid();
    private readonly Guid _driverBDriverId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();

    private readonly string _adminEmail = TestDataFactory.CreateEmail("inc_admin");
    private readonly string _driverAEmail = TestDataFactory.CreateEmail("inc_driver_a");
    private readonly string _driverBEmail = TestDataFactory.CreateEmail("inc_driver_b");
    private const string DefaultPassword = "dev-only-insecure-password-123!";

    private string _adminToken = string.Empty;
    private string _driverAToken = string.Empty;
    private string _driverBToken = string.Empty;

    public IncidentIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new ComplianceTestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var adminUser = new User(_adminUserId, new EmailAddress(_adminEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Admin, "Admin Incident");
        var driverAUser = new User(_driverAUserId, new EmailAddress(_driverAEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Dave");
        var driverBUser = new User(_driverBUserId, new EmailAddress(_driverBEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Bob");

        var driverA = new Driver(
            _driverADriverId,
            _driverAUserId,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(35m),
            new Money(45m),
            new Money(0.85m),
            "ENC(021111111)",
            "ENC(123 Main St)",
            "ENC(Emergency Contact A)",
            new DateOnly(2025, 1, 1),
            DriverStatus.Active);

        var driverB = new Driver(
            _driverBDriverId,
            _driverBUserId,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(35m),
            new Money(45m),
            new Money(0.85m),
            "ENC(021222222)",
            "ENC(456 Queen St)",
            "ENC(Emergency Contact B)",
            new DateOnly(2025, 1, 1),
            DriverStatus.Active);

        var vehicle = new Vehicle(
            _vehicleId,
            TestDataFactory.CreateRegoObject("I"),
            "Isuzu",
            "NPR 250",
            2023,
            "ENC(VIN123)",
            new Kilometres(20000),
            new Kilometres(10000));

        context.Users.AddRange(adminUser, driverAUser, driverBUser);
        context.Drivers.AddRange(driverA, driverB);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        _adminToken = CreateAccessToken(_adminUserId, _adminEmail, UserRole.Admin, "Admin Incident");
        _driverAToken = CreateAccessToken(_driverAUserId, _driverAEmail, UserRole.Driver, "Driver Dave");
        _driverBToken = CreateAccessToken(_driverBUserId, _driverBEmail, UserRole.Driver, "Driver Bob");
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        try
        {
            await using var context = _fixture.CreateDbContext();
            var testDriverIds = new[] { _driverADriverId, _driverBDriverId };
            var incidents = await context.IncidentReports.Where(i => testDriverIds.Contains(i.DriverId)).ToListAsync();
            context.IncidentReports.RemoveRange(incidents);

            var vehicles = await context.Vehicles.Where(v => v.Id == _vehicleId).ToListAsync();
            context.Vehicles.RemoveRange(vehicles);

            var drivers = await context.Drivers.Where(d => testDriverIds.Contains(d.Id)).ToListAsync();
            context.Drivers.RemoveRange(drivers);

            var testUserIds = new[] { _adminUserId, _driverAUserId, _driverBUserId };
            var users = await context.Users.Where(u => testUserIds.Contains(u.Id)).ToListAsync();
            context.Users.RemoveRange(users);

            await context.SaveChangesAsync();
        }
        catch
        {
            // 忽略清理异常
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    #region F9.1: 司机与管理端均可提交事故报告

    [Fact]
    public async Task F9_1_Driver_And_Admin_Can_Submit_IncidentReport_With_Photos_And_Details()
    {
        // 1. 司机提交
        var driverRequest = new ReportIncidentRequest(
            DriverId: null,
            VehicleId: _vehicleId,
            OccurredAt: new DateTimeOffset(2026, 8, 20, 14, 30, 0, TimeSpan.FromHours(12)),
            Location: "Customs Street East, Auckland CBD",
            Severity: IncidentSeverity.Moderate,
            Description: "Side mirror clipped loading bay overhang",
            PhotoKeys: new List<string> { "incidents/site_1.jpg", "incidents/site_2.jpg" },
            ThirdPartyInfo: "ThirdParty_Rego_XYZ123_Name_Alice");

        var driverResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Post, "/api/incidents", driverRequest);
        driverResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var incidentAId = await driverResp.Content.ReadFromJsonAsync<Guid>();
        incidentAId.Should().NotBeEmpty();

        // 2. 管理员代司机提交
        var adminRequest = new ReportIncidentRequest(
            DriverId: _driverBDriverId,
            VehicleId: _vehicleId,
            OccurredAt: new DateTimeOffset(2026, 8, 21, 9, 15, 0, TimeSpan.FromHours(12)),
            Location: "Great South Road / Penrose, Auckland",
            Severity: IncidentSeverity.Minor,
            Description: "Scraped tailgate while reversing into dock",
            PhotoKeys: new List<string> { "incidents/site_3.jpg" });

        var adminResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/incidents", adminRequest);
        adminResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var incidentBId = await adminResp.Content.ReadFromJsonAsync<Guid>();
        incidentBId.Should().NotBeEmpty();

        await using var context = _fixture.CreateDbContext();
        var incA = await context.IncidentReports.FindAsync(incidentAId);
        incA.Should().NotBeNull();
        incA!.DriverId.Should().Be(_driverADriverId);
        incA.Location.Should().Be("Customs Street East, Auckland CBD");
        incA.PhotoKeys.Should().HaveCount(2);

        var incB = await context.IncidentReports.FindAsync(incidentBId);
        incB.Should().NotBeNull();
        incB!.DriverId.Should().Be(_driverBDriverId);
    }

    #endregion

    #region F9.2: 严重度 ≥ Moderate 自动发领域事件并记录 InsurerNotifiedAt；Minor 不发

    [Fact]
    public async Task F9_2_Incident_Severity_Rules_Moderate_Emits_Event_And_Sets_InsurerNotifiedAt_Minor_Does_Not()
    {
        // 1. Moderate 事故：必须记录 InsurerNotifiedAt 并在 Outbox 产生 IncidentReported 消息
        var moderateReq = new ReportIncidentRequest(
            DriverId: null,
            VehicleId: _vehicleId,
            OccurredAt: new DateTimeOffset(2026, 8, 22, 11, 0, 0, TimeSpan.FromHours(12)),
            Location: "State Highway 1 near Highbrook",
            Severity: IncidentSeverity.Moderate,
            Description: "Rear-ended third party in heavy rain",
            PhotoKeys: new List<string> { "incidents/mod.jpg" });

        var modResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Post, "/api/incidents", moderateReq);
        modResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var modIncidentId = await modResp.Content.ReadFromJsonAsync<Guid>();

        // 2. Minor 事故：InsurerNotifiedAt 为 null，Outbox 无 IncidentReported 消息
        var minorReq = new ReportIncidentRequest(
            DriverId: null,
            VehicleId: _vehicleId,
            OccurredAt: new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.FromHours(12)),
            Location: "Depot bay 4",
            Severity: IncidentSeverity.Minor,
            Description: "Bumper rubber rub",
            PhotoKeys: new List<string> { "incidents/min.jpg" });

        var minResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Post, "/api/incidents", minorReq);
        minResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var minIncidentId = await minResp.Content.ReadFromJsonAsync<Guid>();

        // Assert 数据库状态与 Outbox
        await using var verifyContext = _fixture.CreateDbContext();

        var modIncident = await verifyContext.IncidentReports.FindAsync(modIncidentId);
        modIncident.Should().NotBeNull();
        modIncident!.InsurerNotifiedAt.Should().NotBeNull("严重度 ≥ Moderate 必须自动标记 InsurerNotifiedAt");

        var modOutbox = await verifyContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type == "IncidentReported" && m.PayloadJson.Contains(modIncidentId.ToString()));
        modOutbox.Should().NotBeNull("严重度 ≥ Moderate 必须向 Outbox 发送 IncidentReported 领域事件");

        var minIncident = await verifyContext.IncidentReports.FindAsync(minIncidentId);
        minIncident.Should().NotBeNull();
        minIncident!.InsurerNotifiedAt.Should().BeNull("Minor 事故不得自动标记 InsurerNotifiedAt");

        var minOutbox = await verifyContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type == "IncidentReported" && m.PayloadJson.Contains(minIncidentId.ToString()));
        minOutbox.Should().BeNull("Minor 事故不得向 Outbox 发送 IncidentReported 领域事件");
    }

    #endregion

    #region F9.3: 第三方信息落库为密文（psql 直查看不到明文）

    /// <summary>
    /// F9.3 核心验收标准：
    /// 第三方信息在 DB 中以 AES-256-GCM 密文存储，使用原生 SQL 直查绝对看不到明文，
    /// 经过应用层授权查询则自动解密为明文。
    /// </summary>
    [Fact]
    public async Task F9_3_ThirdPartyInfo_Is_Encrypted_In_Database_And_Psql_Query_Shows_Only_Ciphertext()
    {
        // Arrange
        const string plainThirdPartyInfo = "Rego: ABC123_Name: John Doe_Phone: +64 21 555 7890";
        var request = new ReportIncidentRequest(
            DriverId: null,
            VehicleId: _vehicleId,
            OccurredAt: new DateTimeOffset(2026, 8, 23, 16, 0, 0, TimeSpan.FromHours(12)),
            Location: "Ti Rakau Drive, East Tamaki",
            Severity: IncidentSeverity.Major,
            Description: "Sideswipe with parked vehicle",
            PhotoKeys: new List<string> { "incidents/major_1.jpg" },
            ThirdPartyInfo: plainThirdPartyInfo);

        // Act 1: 提交事故报告
        var response = await SendAuthorizedAsync(_driverAToken, HttpMethod.Post, "/api/incidents", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var incidentId = await response.Content.ReadFromJsonAsync<Guid>();

        // Assert 1: 原生 SQL 直查（模拟 psql 命令行直查）—— 数据库中必须为密文，绝无明文
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using (var cmd = new NpgsqlCommand("SELECT \"ThirdPartyInfoEnc\" FROM \"IncidentReports\" WHERE \"Id\" = @id", connection))
        {
            cmd.Parameters.AddWithValue("id", incidentId);
            var rawValue = (string?)(await cmd.ExecuteScalarAsync());

            rawValue.Should().NotBeNullOrWhiteSpace("数据库中 ThirdPartyInfoEnc 列必须已落库");
            rawValue.Should().StartWith("enc:v1:", "加密字段在数据库中必须带有显式版本前缀 enc:v1:");
            rawValue.Should().NotBe(plainThirdPartyInfo, "数据库中绝不能存储明文信息");
            rawValue.Should().NotContain("John Doe", "psql 直查绝对不包含对方姓名明文");
            rawValue.Should().NotContain("ABC123", "psql 直查绝对不包含对方车牌明文");
            rawValue.Should().NotContain("+64 21 555 7890", "psql 直查绝对不包含对方电话明文");
        }

        // Assert 2: 通过 API 端点 GET /api/incidents/{id} 授权读取 —— 经 EF Core ValueConverter 自动解密回明文
        var getDetailResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, $"/api/incidents/{incidentId}");
        getDetailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await getDetailResp.Content.ReadFromJsonAsync<IncidentReportDetailDto>();

        detail.Should().NotBeNull();
        detail!.ThirdPartyInfo.Should().Be(plainThirdPartyInfo, "授权查询读取时应由 ValueConverter 自动解密回原始明文");
    }

    #endregion

    #region F9.4: 按车辆/司机/时间范围查询事故历史（用于理赔）与越权拦截

    [Fact]
    public async Task F9_4_QueryIncidentHistory_FiltersByVehicleAndDate_AndBlocksIDORWith403()
    {
        // 1. Arrange: 录入特定时段的事故
        var date1 = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.FromHours(12));
        var date2 = new DateTimeOffset(2026, 8, 15, 14, 0, 0, TimeSpan.FromHours(12));

        var inc1 = new IncidentReport(Guid.NewGuid(), _driverADriverId, _vehicleId, date1, "Loc 1", IncidentSeverity.Minor, "Desc 1");
        var inc2 = new IncidentReport(Guid.NewGuid(), _driverADriverId, _vehicleId, date2, "Loc 2", IncidentSeverity.Major, "Desc 2");
        var incOther = new IncidentReport(Guid.NewGuid(), _driverBDriverId, _vehicleId, date2, "Loc 3", IncidentSeverity.Minor, "Desc 3");

        await using (var context = _fixture.CreateDbContext())
        {
            context.IncidentReports.AddRange(inc1, inc2, incOther);
            await context.SaveChangesAsync();
        }

        // 2. Act 1: 管理端按车辆 + 时间范围查询
        var adminQueryUrl = $"/api/incidents?vehicleId={_vehicleId}&fromDate=2026-08-10T00:00:00%2B12:00&toDate=2026-08-20T23:59:59%2B12:00";
        var adminResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, adminQueryUrl);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminResult = await adminResp.Content.ReadFromJsonAsync<PagedResult<IncidentReportDto>>();

        adminResult.Should().NotBeNull();
        adminResult!.Items.Should().Contain(i => i.Id == inc2.Id);
        adminResult.Items.Should().Contain(i => i.Id == incOther.Id);
        adminResult.Items.Should().NotContain(i => i.Id == inc1.Id, "早于筛选时间区间的记录不应返回");

        // 3. Act 2: IDOR 拦截 —— 司机 A 尝试查询司机 B 的事故列表
        var driverQueryOtherResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, $"/api/incidents?driverId={_driverBDriverId}");
        driverQueryOtherResp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "司机尝试查询其他司机的事故历史必须 403");

        // 4. Act 3: IDOR 拦截 —— 司机 A 尝试按 ID 查询司机 B 的事故详情
        var driverDetailOtherResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, $"/api/incidents/{incOther.Id}");
        driverDetailOtherResp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "司机尝试查看其他司机的事故详情必须 403");
    }

    #endregion

    private string CreateAccessToken(Guid userId, string email, UserRole role, string displayName)
    {
        var jwtGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = jwtGenerator.GenerateAccessToken(userId, email, role.ToString(), displayName);
        return token;
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
