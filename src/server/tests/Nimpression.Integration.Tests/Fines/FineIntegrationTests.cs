using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Api.Endpoints;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Fines.DTOs;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Fines;

[Collection("PostgreSqlCollection")]
public sealed class FineIntegrationTests : IAsyncLifetime, IDisposable
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

    private readonly string _adminEmail = TestDataFactory.CreateEmail("fine_admin");
    private readonly string _driverAEmail = TestDataFactory.CreateEmail("fine_driver_a");
    private readonly string _driverBEmail = TestDataFactory.CreateEmail("fine_driver_b");
    private const string DefaultPassword = "Password123!";

    private string _adminToken = string.Empty;
    private string _driverAToken = string.Empty;
    private string _driverBToken = string.Empty;

    public FineIntegrationTests(PostgreSqlContainerFixture fixture)
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

        var adminUser = new User(_adminUserId, new EmailAddress(_adminEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Admin, "Admin Fine");
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
            TestDataFactory.CreateRegoObject("F"),
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

        _adminToken = CreateAccessToken(_adminUserId, _adminEmail, UserRole.Admin, "Admin Fine");
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
            var fines = await context.Fines.Where(f => testDriverIds.Contains(f.DriverId)).ToListAsync();
            context.Fines.RemoveRange(fines);

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

    #region F8.1: 罚单提交与数据持久化

    [Fact]
    public async Task F8_1_Driver_SubmitsFine_Succeeds_PersistsToDb_WithInitialSubmittedStatus()
    {
        // Arrange
        var request = new SubmitFineRequest(
            DriverId: null,
            VehicleId: _vehicleId,
            IssuedOn: new DateOnly(2026, 8, 15),
            Authority: "NZ Police",
            Reference: "INF-202608-1001",
            Amount: 150.00m,
            Currency: "NZD",
            Reason: "Exceeding 50 km/h in urban zone (58 km/h detected)",
            TicketPhotoKey: "fines/sample_ticket_1.jpg");

        // Act
        var response = await SendAuthorizedAsync(_driverAToken, HttpMethod.Post, "/api/fines", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var fineId = await response.Content.ReadFromJsonAsync<Guid>();
        fineId.Should().NotBeEmpty();

        await using var context = _fixture.CreateDbContext();
        var fine = await context.Fines.FindAsync(fineId);
        fine.Should().NotBeNull();
        fine!.DriverId.Should().Be(_driverADriverId);
        fine.VehicleId.Should().Be(_vehicleId);
        fine.Authority.Should().Be("NZ Police");
        fine.Reference.Should().Be("INF-202608-1001");
        fine.Amount.Amount.Should().Be(150.00m);
        fine.Amount.Currency.Should().Be("NZD");
        fine.Reason.Should().Be("Exceeding 50 km/h in urban zone (58 km/h detected)");
        fine.TicketPhotoKey.Should().Be("fines/sample_ticket_1.jpg");
        fine.Status.Should().Be(FineStatus.Submitted);
    }

    #endregion

    #region F8.2: 状态流转状态机与 422 拒绝

    [Fact]
    public async Task F8_2_FineStateMachine_Submitted_To_UnderReview_To_Accepted_Disputed_Waived_And_Rejects_InvalidTransitions_With_422()
    {
        // 1. Arrange: 创建初始 Submitted 态罚单
        var fineId = Guid.NewGuid();
        var fine = new Fine(
            fineId,
            _driverADriverId,
            _vehicleId,
            new DateOnly(2026, 8, 10),
            "Auckland Transport",
            "AT-202608-2001",
            new Money(80m),
            "Bus lane violation",
            "fines/ticket_review.jpg");

        await using (var context = _fixture.CreateDbContext())
        {
            context.Fines.Add(fine);
            await context.SaveChangesAsync();
        }

        // 2. 负向测试: Submitted 态直接调用 Accept 必须返回 422 UnprocessableEntity
        var invalidAcceptResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/fines/{fineId}/accept",
            new AcceptFineRequest("Premature accept"));
        invalidAcceptResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 3. 正向测试: Submitted -> UnderReview
        var startReviewResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/fines/{fineId}/start-review");
        startReviewResp.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var context = _fixture.CreateDbContext())
        {
            var underReviewFine = await context.Fines.FindAsync(fineId);
            underReviewFine!.Status.Should().Be(FineStatus.UnderReview);
            underReviewFine.ReviewedByUserId.Should().Be(_adminUserId);
        }

        // 4. 重复 start-review -> 422
        var repeatReviewResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/fines/{fineId}/start-review");
        repeatReviewResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 5. 正向测试: UnderReview -> Accepted
        var acceptResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/fines/{fineId}/accept",
            new AcceptFineRequest("Driver liability acknowledged"));
        acceptResp.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var context = _fixture.CreateDbContext())
        {
            var acceptedFine = await context.Fines.FindAsync(fineId);
            acceptedFine!.Status.Should().Be(FineStatus.Accepted);
            acceptedFine.ReviewNote.Should().Be("Driver liability acknowledged");
        }

        // 6. 终态不可逆: Accepted 态再次调用 Dispute/Waive -> 422
        var disputeResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/fines/{fineId}/dispute",
            new DisputeFineRequest("Disputing already accepted fine"));
        disputeResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var waiveResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/fines/{fineId}/waive",
            new WaiveFineRequest("Waiving already accepted fine"));
        waiveResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    #endregion

    #region F8.3: Accepted 状态触发 Outbox 领域事件

    [Fact]
    public async Task F8_3_AcceptFine_Persists_FineAccepted_DomainEvent_To_Outbox()
    {
        // Arrange: 创建 UnderReview 态罚单
        var fineId = Guid.NewGuid();
        var fine = new Fine(
            fineId,
            _driverADriverId,
            _vehicleId,
            new DateOnly(2026, 8, 12),
            "NZTA Waka Kotahi",
            "NZTA-202608-3001",
            new Money(200m),
            "COF violation",
            "fines/ticket_outbox.jpg");
        fine.StartReview(_adminUserId, new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.FromHours(12)));

        await using (var context = _fixture.CreateDbContext())
        {
            context.Fines.Add(fine);
            await context.SaveChangesAsync();
        }

        // Act: 管理员接受罚单
        var response = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/fines/{fineId}/accept",
            new AcceptFineRequest("Insurance claim processed"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: 验证 OutboxMessages 表中成功写入 FineAccepted 领域事件
        await using var verifyContext = _fixture.CreateDbContext();
        var outboxMessage = await verifyContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type == "FineAccepted" && m.PayloadJson.Contains(fineId.ToString()));

        outboxMessage.Should().NotBeNull();
        outboxMessage!.PayloadJson.Should().Contain(_driverADriverId.ToString());
        outboxMessage.PayloadJson.Should().Contain(_vehicleId.ToString());
    }

    #endregion

    #region F8.4: 越权取他人罚单照片必须 403 Forbidden（最关键负向用例）

    /// <summary>
    /// F8.4 最核心验收标准：
    /// 司机 A 尝试使用司机 B 的罚单 Id 请求短时效预签名照片 URL，必须返回 403 Forbidden（绝对不是 404）。
    /// </summary>
    [Fact]
    public async Task F8_4_IDOR_DriverA_Requests_DriverB_FinePhoto_Returns_403_Forbidden()
    {
        // Arrange: 为司机 B 创建一张罚单
        var fineBId = Guid.NewGuid();
        var fineB = new Fine(
            fineBId,
            _driverBDriverId, // 属于司机 B
            _vehicleId,
            new DateOnly(2026, 8, 14),
            "NZ Police",
            "REF-BOB-001",
            new Money(150m),
            "Speeding",
            "fines/bob_ticket.jpg");

        await using (var context = _fixture.CreateDbContext())
        {
            context.Fines.Add(fineB);
            await context.SaveChangesAsync();
        }

        // Act 1: 司机 A 发起请求获取司机 B 的罚单照片预签名 URL: GET /api/fines/{fineBId}/photo
        var photoResponse = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, $"/api/fines/{fineBId}/photo");

        // Assert 1: 必须返回 403 Forbidden（非 404）
        photoResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "司机禁止获取他人罚单照片，越权必须返回 403 而非 404");

        // Act 2: 司机 A 发起请求获取司机 B 的罚单详情: GET /api/fines/{fineBId}
        var detailResponse = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, $"/api/fines/{fineBId}");

        // Assert 2: 同样必须返回 403 Forbidden
        detailResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "司机禁止获取他人罚单详情，越权必须返回 403");

        // Act 3: 司机 B 请求自己的罚单照片: 应该成功 (200 OK)
        var ownPhotoResp = await SendAuthorizedAsync(_driverBToken, HttpMethod.Get, $"/api/fines/{fineBId}/photo");
        ownPhotoResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ownPhotoResp.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("url", out var urlProp).Should().BeTrue();
        urlProp.GetString().Should().NotBeNullOrWhiteSpace();
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

public sealed class ComplianceTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ComplianceTestWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
            });
        });
    }
}
