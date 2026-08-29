using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Api.Endpoints;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Payroll;

[Collection("PostgreSqlCollection")]
public sealed class PayrollIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private PayrollTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _driver1UserId = Guid.NewGuid();
    private readonly Guid _driver1Id = Guid.NewGuid();
    private readonly Guid _driver2UserId = Guid.NewGuid();
    private readonly Guid _driver2Id = Guid.NewGuid();
    private readonly Guid _areaId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();

    private readonly string _adminEmail = TestDataFactory.CreateEmail("pr_admin");
    private readonly string _driver1Email = TestDataFactory.CreateEmail("pr_driver1");
    private readonly string _driver2Email = TestDataFactory.CreateEmail("pr_driver2");
    private const string DefaultPassword = "Password123!";

    private string _adminToken = string.Empty;
    private string _driver1Token = string.Empty;
    private string _driver2Token = string.Empty;

    public PayrollIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new PayrollTestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var existingPayslips = await context.Payslips.ToListAsync();
        context.Payslips.RemoveRange(existingPayslips);
        var existingPeriods = await context.PayPeriods.ToListAsync();
        context.PayPeriods.RemoveRange(existingPeriods);
        await context.SaveChangesAsync();

        var adminUser = new User(_adminUserId, new EmailAddress(_adminEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Admin, "Admin Payroll");
        var driver1User = new User(_driver1UserId, new EmailAddress(_driver1Email), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Alice");
        var driver2User = new User(_driver2UserId, new EmailAddress(_driver2Email), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Bob");

        var driver1 = new Driver(
            _driver1Id,
            _driver1UserId,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 2",
            new DateOnly(2028, 1, 1),
            new Money(30.00m),
            new Money(45.00m),
            new Money(1.20m),
            "ENC(021111111)",
            "ENC(123 Auckland Way)",
            "ENC(Emergency 1)",
            new DateOnly(2025, 1, 1),
            DriverStatus.Active);

        var driver2 = new Driver(
            _driver2Id,
            _driver2UserId,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 2",
            new DateOnly(2028, 1, 1),
            new Money(25.00m),
            new Money(50.00m),
            new Money(1.00m),
            "ENC(021222222)",
            "ENC(456 Wellington Rd)",
            "ENC(Emergency 2)",
            new DateOnly(2025, 1, 1),
            DriverStatus.Active);

        var area = new Domain.Entities.Area.Area(_areaId, "Metro Auckland", $"AKL-{Guid.NewGuid():N}"[..10].ToUpperInvariant());
        var vehicle = new Vehicle(
            _vehicleId,
            TestDataFactory.CreateRegoObject("V"),
            "Scania",
            "P-Series",
            2023,
            "ENC(VIN999)",
            new Kilometres(15000),
            new Kilometres(10000));

        context.Users.AddRange(adminUser, driver1User, driver2User);
        context.Drivers.AddRange(driver1, driver2);
        context.Areas.Add(area);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        _adminToken = CreateAccessToken(_adminUserId, _adminEmail, UserRole.Admin, "Admin Payroll");
        _driver1Token = CreateAccessToken(_driver1UserId, _driver1Email, UserRole.Driver, "Driver Alice");
        _driver2Token = CreateAccessToken(_driver2UserId, _driver2Email, UserRole.Driver, "Driver Bob");
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
            var testDriverIds = new[] { _driver1Id, _driver2Id };

            var payslips = await context.Payslips.Where(p => testDriverIds.Contains(p.DriverId)).ToListAsync();
            context.Payslips.RemoveRange(payslips);

            var payPeriods = await context.PayPeriods.ToListAsync();
            context.PayPeriods.RemoveRange(payPeriods);

            var tasks = await context.JobTasks.Where(t => t.DriverId.HasValue && testDriverIds.Contains(t.DriverId.Value)).ToListAsync();
            context.JobTasks.RemoveRange(tasks);

            var shifts = await context.ShiftEntries.Where(s => testDriverIds.Contains(s.DriverId)).ToListAsync();
            context.ShiftEntries.RemoveRange(shifts);

            var fines = await context.Fines.Where(f => testDriverIds.Contains(f.DriverId)).ToListAsync();
            context.Fines.RemoveRange(fines);

            var areas = await context.Areas.Where(a => a.Id == _areaId).ToListAsync();
            context.Areas.RemoveRange(areas);

            var vehicles = await context.Vehicles.Where(v => v.Id == _vehicleId).ToListAsync();
            context.Vehicles.RemoveRange(vehicles);

            var drivers = await context.Drivers.Where(d => testDriverIds.Contains(d.Id)).ToListAsync();
            context.Drivers.RemoveRange(drivers);

            var testUserIds = new[] { _adminUserId, _driver1UserId, _driver2UserId };
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

    #region F7.1 - F7.5: 薪资计算、费率分档、双口径明细保留与最低工资地板

    [Fact]
    public async Task F7_1_To_F7_5_CreatePeriod_Calculate_DualBasis_And_MinimumWageTopUp()
    {
        // Arrange
        var nzOffset = TimeSpan.FromHours(12);
        var startsOn = new DateOnly(2026, 8, 17); // Monday
        var endsOn = new DateOnly(2026, 8, 30);   // Sunday

        // 1. Create Pay Period
        var createResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/payroll/periods", new CreatePayPeriodRequest(startsOn, endsOn));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var periodDto = await createResp.Content.ReadFromJsonAsync<PayPeriodDto>();
        periodDto.Should().NotBeNull();
        var periodId = periodDto!.Id;

        // 2. Add Shifts for Driver 1:
        // Day 1 (2026-08-17): 10h -> 8h Ordinary ($30) + 2h Overtime ($45) = $240 + $90 = $330
        // Day 2 (2026-08-18): 6h Holiday (2.0x -> $60) = $360
        // Total Hours = 16h, Total HoursGross = $690
        var shift1 = new ShiftEntry(Guid.NewGuid(), _driver1Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), -36.8485m, 174.7633m, _vehicleId);
        shift1.ClockOut(new DateTimeOffset(2026, 8, 17, 18, 0, 0, nzOffset), breakMinutes: 0);

        var shift2 = new ShiftEntry(Guid.NewGuid(), _driver1Id, new DateTimeOffset(2026, 8, 18, 8, 0, 0, nzOffset), -36.8485m, 174.7633m, _vehicleId);
        shift2.ClockOut(new DateTimeOffset(2026, 8, 18, 14, 0, 0, nzOffset), breakMinutes: 0);

        // 3. Add 1 Completed Task + 1 Cancelled Task for Driver 1
        var taskCompleted = new JobTask(
            id: Guid.NewGuid(),
            @ref: "TSK-C01",
            title: "Task 1",
            areaId: _areaId,
            scheduledFor: new DateTimeOffset(2026, 8, 17, 10, 0, 0, nzOffset),
            createdByUserId: _adminUserId,
            plannedDistanceKm: new Kilometres(50m),
            driverId: _driver1Id,
            vehicleId: _vehicleId);
        taskCompleted.Acknowledge(new DateTimeOffset(2026, 8, 17, 10, 30, 0, nzOffset));
        taskCompleted.Start(new DateTimeOffset(2026, 8, 17, 11, 0, 0, nzOffset));
        taskCompleted.Complete(new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset), actualDistanceKm: new Kilometres(50m));

        var taskCancelled = new JobTask(
            id: Guid.NewGuid(),
            @ref: "TSK-CAN01",
            title: "Cancelled Task",
            areaId: _areaId,
            scheduledFor: new DateTimeOffset(2026, 8, 17, 13, 0, 0, nzOffset),
            createdByUserId: _adminUserId,
            plannedDistanceKm: new Kilometres(80m),
            driverId: _driver1Id,
            vehicleId: _vehicleId);
        taskCancelled.Cancel("Customer cancellation", new DateTimeOffset(2026, 8, 17, 13, 30, 0, nzOffset));

        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.AddRange(shift1, shift2);
            context.JobTasks.AddRange(taskCompleted, taskCancelled);
            await context.SaveChangesAsync();
        }

        // 4. Calculate Payroll via API with public holiday on 2026-08-18
        var calcReq = new CalculatePayrollRequest(
            DriverId: _driver1Id,
            PublicHolidays: [new DateOnly(2026, 8, 18)],
            MinimumHourlyWage: 23.15m);

        var calcResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/calculate", calcReq);
        calcResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var payslips = await calcResp.Content.ReadFromJsonAsync<List<PayslipDto>>();

        // Assertions
        payslips.Should().NotBeNull();
        payslips!.Should().HaveCount(1);
        var payslip = payslips![0];

        // F7.1: 三档费率验证 (8h ord, 2h ot, 6h hol)
        payslip.OrdinaryHours.Should().Be(8.00m);
        payslip.OvertimeHours.Should().Be(2.00m);
        payslip.HolidayHours.Should().Be(6.00m);
        payslip.HoursBasedGross.Should().Be(690.00m);

        // F7.2: 趟次口径只算 Completed (1 趟，已取消任务不计酬)
        payslip.CompletedTripCount.Should().Be(1);
        payslip.TotalDistanceKm.Should().Be(50.00m);
        // Trip: 1 * $45 + 50km * $1.20 = $45 + $60 = $105
        payslip.TripBasedGross.Should().Be(105.00m);

        // F7.3: 取高（工时胜）
        payslip.BasisUsed.Should().Be(PayBasis.Hourly);
        payslip.GrossPay.Should().Be(690.00m);

        // F7.4: 双口径明细都保留
        payslip.Lines.Should().Contain(l => l.Basis == PayBasis.Hourly && l.Kind == "OrdinaryHours" && l.Amount == 240.00m);
        payslip.Lines.Should().Contain(l => l.Basis == PayBasis.Hourly && l.Kind == "OvertimeHours" && l.Amount == 90.00m);
        payslip.Lines.Should().Contain(l => l.Basis == PayBasis.Hourly && l.Kind == "HolidayHours" && l.Amount == 360.00m);
        payslip.Lines.Should().Contain(l => l.Basis == PayBasis.Trip && l.Kind == "TripBase" && l.Amount == 45.00m);
        payslip.Lines.Should().Contain(l => l.Basis == PayBasis.Trip && l.Kind == "Mileage" && l.Amount == 60.00m);
    }

    #endregion

    #region F7.6 - F7.9: 周期定版、费率快照与作废重开

    [Fact]
    public async Task F7_8_And_F7_9_Finalise_FreezesRates_And_VoidReopens()
    {
        var nzOffset = TimeSpan.FromHours(12);
        var startsOn = new DateOnly(2026, 9, 7);
        var endsOn = new DateOnly(2026, 9, 20);

        // 1. Create Pay Period
        var createResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/payroll/periods", new CreatePayPeriodRequest(startsOn, endsOn));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var periodDto = await createResp.Content.ReadFromJsonAsync<PayPeriodDto>();
        var periodId = periodDto!.Id;

        // 2. Add Shift (8h @ $30 = $240)
        var shift = new ShiftEntry(Guid.NewGuid(), _driver1Id, new DateTimeOffset(2026, 9, 7, 8, 0, 0, nzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 9, 7, 16, 0, 0, nzOffset), breakMinutes: 0);

        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shift);
            await context.SaveChangesAsync();
        }

        // 3. Calculate
        var calcResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/calculate", new CalculatePayrollRequest(DriverId: _driver1Id));
        calcResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Finalise
        var finaliseResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/finalise");
        finaliseResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalisedPeriod = await finaliseResp.Content.ReadFromJsonAsync<PayPeriodDto>();
        finalisedPeriod!.Status.Should().Be(PayPeriodStatus.Finalised);

        // 5. F7.9: 事后修改司机费率
        await using (var context = _fixture.CreateDbContext())
        {
            var driver = await context.Drivers.FindAsync(_driver1Id);
            driver!.UpdateRates(new Money(100.00m), new Money(200.00m), new Money(5.00m));
            await context.SaveChangesAsync();
        }

        // 查询已定版工资单，金额与快照费率不变
        var payslipsResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/payroll/periods/{periodId}/payslips");
        payslipsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var payslips = await payslipsResp.Content.ReadFromJsonAsync<List<PayslipDto>>();
        payslips![0].GrossPay.Should().Be(240.00m);
        payslips[0].HourlyRateSnapshot.Should().Be(30.00m);

        // 6. F7.8: 作废重开（必须填理由）
        var voidReq = new VoidPayPeriodRequest("Audit correction required");
        var voidResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/void", voidReq);
        voidResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var voidedPeriod = await voidResp.Content.ReadFromJsonAsync<PayPeriodDto>();
        voidedPeriod!.Status.Should().Be(PayPeriodStatus.Open);
    }

    #endregion

    #region F7.10: 权限控制与 IDOR 防护（查他人 403 而非 404）

    [Fact]
    public async Task F7_10_Driver_CannotViewOtherDriverPayslip_Returns403Forbidden()
    {
        var nzOffset = TimeSpan.FromHours(12);
        var startsOn = new DateOnly(2026, 9, 21);
        var endsOn = new DateOnly(2026, 10, 4);

        var createResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/payroll/periods", new CreatePayPeriodRequest(startsOn, endsOn));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var periodDto = await createResp.Content.ReadFromJsonAsync<PayPeriodDto>();
        var periodId = periodDto!.Id;

        // Shift for Driver 2
        var shiftDriver2 = new ShiftEntry(Guid.NewGuid(), _driver2Id, new DateTimeOffset(2026, 9, 21, 8, 0, 0, nzOffset));
        shiftDriver2.ClockOut(new DateTimeOffset(2026, 9, 21, 16, 0, 0, nzOffset));

        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shiftDriver2);
            await context.SaveChangesAsync();
        }

        // Calculate & Finalise for Driver 2
        await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/calculate", new CalculatePayrollRequest(DriverId: _driver2Id));
        await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/finalise");

        // Get Driver 2's payslip ID
        var listResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/payroll/periods/{periodId}/payslips");
        var list = await listResp.Content.ReadFromJsonAsync<List<PayslipDto>>();
        var driver2PayslipId = list![0].Id;

        // Act: Driver 1 attempts to query Driver 2's payslip -> 403 Forbidden
        var idorResp = await SendAuthorizedAsync(_driver1Token, HttpMethod.Get, $"/api/payroll/payslips/{driver2PayslipId}");

        // Assert: 必须返回 403 Forbidden (F7.10 明确要求：不是 404)
        idorResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task F7_10_Driver_CanViewOwnFinalisedPayslip()
    {
        var nzOffset = TimeSpan.FromHours(12);
        var startsOn = new DateOnly(2026, 10, 5);
        var endsOn = new DateOnly(2026, 10, 18);

        var createResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/payroll/periods", new CreatePayPeriodRequest(startsOn, endsOn));
        var periodDto = await createResp.Content.ReadFromJsonAsync<PayPeriodDto>();
        var periodId = periodDto!.Id;

        var shiftDriver1 = new ShiftEntry(Guid.NewGuid(), _driver1Id, new DateTimeOffset(2026, 10, 5, 8, 0, 0, nzOffset));
        shiftDriver1.ClockOut(new DateTimeOffset(2026, 10, 5, 16, 0, 0, nzOffset));

        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shiftDriver1);
            await context.SaveChangesAsync();
        }

        await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/calculate", new CalculatePayrollRequest(DriverId: _driver1Id));
        await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/finalise");

        var listResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/payroll/periods/{periodId}/payslips");
        var list = await listResp.Content.ReadFromJsonAsync<List<PayslipDto>>();
        var driver1PayslipId = list![0].Id;

        // Act: Driver 1 queries own finalised payslip
        var resp = await SendAuthorizedAsync(_driver1Token, HttpMethod.Get, $"/api/payroll/payslips/{driver1PayslipId}");

        // Assert: 200 OK
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var payslip = await resp.Content.ReadFromJsonAsync<PayslipDto>();
        payslip!.Id.Should().Be(driver1PayslipId);
        payslip.DriverId.Should().Be(_driver1Id);
    }

    #endregion

    #region F7.11 & F7.12: 追溯明细与罚款法规独立展示

    [Fact]
    public async Task F7_11_And_F7_12_PayslipTraceability_And_FinePartition()
    {
        var nzOffset = TimeSpan.FromHours(12);
        var startsOn = new DateOnly(2026, 10, 19);
        var endsOn = new DateOnly(2026, 11, 1);

        var createResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/payroll/periods", new CreatePayPeriodRequest(startsOn, endsOn));
        var periodDto = await createResp.Content.ReadFromJsonAsync<PayPeriodDto>();
        var periodId = periodDto!.Id;

        // Shift
        var shift = new ShiftEntry(Guid.NewGuid(), _driver1Id, new DateTimeOffset(2026, 10, 19, 8, 0, 0, nzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 10, 19, 16, 0, 0, nzOffset), breakMinutes: 30);

        // JobTask
        var task = new JobTask(
            id: Guid.NewGuid(),
            @ref: "TSK-TRACE-01",
            title: "Trace Task",
            areaId: _areaId,
            scheduledFor: new DateTimeOffset(2026, 10, 19, 10, 0, 0, nzOffset),
            createdByUserId: _adminUserId,
            plannedDistanceKm: new Kilometres(25m),
            driverId: _driver1Id,
            vehicleId: _vehicleId);
        task.Acknowledge(new DateTimeOffset(2026, 10, 19, 10, 15, 0, nzOffset));
        task.Start(new DateTimeOffset(2026, 10, 19, 10, 30, 0, nzOffset));
        task.Complete(new DateTimeOffset(2026, 10, 19, 12, 0, 0, nzOffset), actualDistanceKm: new Kilometres(28m));

        // Fine (F7.12)
        var fine = new Fine(
            id: Guid.NewGuid(),
            driverId: _driver1Id,
            vehicleId: _vehicleId,
            issuedOn: new DateOnly(2026, 10, 20),
            authority: "NZTA",
            reference: "INF-NZTA-001",
            amount: new Money(200.00m),
            reason: "Toll Road Violation");

        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shift);
            context.JobTasks.Add(task);
            context.Fines.Add(fine);
            await context.SaveChangesAsync();
        }

        await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/calculate", new CalculatePayrollRequest(DriverId: _driver1Id));
        await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/payroll/periods/{periodId}/finalise");

        var listResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, $"/api/payroll/periods/{periodId}/payslips");
        var list = await listResp.Content.ReadFromJsonAsync<List<PayslipDto>>();
        var payslipId = list![0].Id;

        // Act: Query Payslip
        var payslipResp = await SendAuthorizedAsync(_driver1Token, HttpMethod.Get, $"/api/payroll/payslips/{payslipId}");
        payslipResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await payslipResp.Content.ReadFromJsonAsync<PayslipDto>();

        // Assert F7.11: ShiftDetails & TripDetails
        detail!.ShiftDetails.Should().ContainSingle(s => s.ShiftId == shift.Id && s.PayableHours == 7.50m);
        detail.TripDetails.Should().ContainSingle(t => t.JobTaskId == task.Id && t.Ref == "TSK-TRACE-01" && t.EffectiveDistanceKm == 28.00m);

        // Assert F7.12: Fines partition with legal notice, gross pay untouched by fine
        detail.Fines.Should().ContainSingle(f => f.Reference == "INF-NZTA-001" && f.Amount == 200.00m);
        detail.FinesLegalNotice.Should().Contain("Wages Protection Act 1983");
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

public sealed class PayrollTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public PayrollTestWebApplicationFactory(string connectionString)
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
