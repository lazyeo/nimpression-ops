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
using Nimpression.Application.Features.Timesheets.DTOs;
using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Seed;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Timesheets;

[Collection("PostgreSqlCollection")]
public sealed class TimesheetIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private TimesheetTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _driverUserId = Guid.NewGuid();
    private readonly Guid _driverId = Guid.NewGuid();
    private readonly Guid _otherDriverUserId = Guid.NewGuid();
    private readonly Guid _otherDriverId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();

    private readonly string _adminEmail = TestDataFactory.CreateEmail("ts_admin");
    private readonly string _driverEmail = TestDataFactory.CreateEmail("ts_driver");
    private readonly string _otherDriverEmail = TestDataFactory.CreateEmail("ts_driver_other");
    private const string DefaultPassword = "Password123!";

    private string _adminToken = string.Empty;
    private string _driverToken = string.Empty;
    private string _otherDriverToken = string.Empty;

    public TimesheetIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new TimesheetTestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var adminUser = new User(_adminUserId, new EmailAddress(_adminEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Admin, "Admin Timesheet");
        var driverUser = new User(_driverUserId, new EmailAddress(_driverEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Dave");
        var otherDriverUser = new User(_otherDriverUserId, new EmailAddress(_otherDriverEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Driver Bob");

        var driver = new Driver(
            _driverId,
            _driverUserId,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(35m),
            new Money(45m),
            new Money(0.85m),
            "ENC(021111111)",
            "ENC(123 Main St)",
            "ENC(Emergency Contact)",
            new DateOnly(2025, 1, 1),
            DriverStatus.Active);

        var otherDriver = new Driver(
            _otherDriverId,
            _otherDriverUserId,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(35m),
            new Money(45m),
            new Money(0.85m),
            "ENC(021222222)",
            "ENC(456 Queen St)",
            "ENC(Emergency Contact 2)",
            new DateOnly(2025, 1, 1),
            DriverStatus.Active);

        var vehicle = new Vehicle(
            _vehicleId,
            TestDataFactory.CreateRegoObject("V"),
            "Isuzu",
            "NPR 250",
            2023,
            "ENC(VIN123)",
            new Kilometres(20000),
            new Kilometres(10000));

        context.Users.AddRange(adminUser, driverUser, otherDriverUser);
        context.Drivers.AddRange(driver, otherDriver);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        _adminToken = CreateAccessToken(_adminUserId, _adminEmail, UserRole.Admin, "Admin Timesheet");
        _driverToken = CreateAccessToken(_driverUserId, _driverEmail, UserRole.Driver, "Driver Dave");
        _otherDriverToken = CreateAccessToken(_otherDriverUserId, _otherDriverEmail, UserRole.Driver, "Driver Bob");
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
            var testDriverIds = new[] { _driverId, _otherDriverId };
            var shifts = await context.ShiftEntries.Where(s => testDriverIds.Contains(s.DriverId)).ToListAsync();
            context.ShiftEntries.RemoveRange(shifts);

            var vehicles = await context.Vehicles.Where(v => v.Id == _vehicleId).ToListAsync();
            context.Vehicles.RemoveRange(vehicles);

            var drivers = await context.Drivers.Where(d => testDriverIds.Contains(d.Id)).ToListAsync();
            context.Drivers.RemoveRange(drivers);

            var testUserIds = new[] { _adminUserId, _driverUserId, _otherDriverUserId };
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

    #region AC F6.1: 上下班打卡与 GPS 降级与 409 冲突

    [Fact]
    public async Task F6_1_ClockIn_WithGps_Returns201Created_AndPersistsCoordinates()
    {
        // Arrange
        var request = new ClockInRequest(
            DriverId: null,
            ClockInAt: new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12)),
            Latitude: -36.8485000m,
            Longitude: 174.7633000m,
            VehicleId: _vehicleId,
            LocationUnavailable: false);

        // Act
        var response = await SendAuthorizedAsync(_driverToken, HttpMethod.Post, "/api/timesheets/clock-in", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var shiftId = await response.Content.ReadFromJsonAsync<Guid>();
        shiftId.Should().NotBeEmpty();

        await using var context = _fixture.CreateDbContext();
        var shift = await context.ShiftEntries.FindAsync(shiftId);
        shift.Should().NotBeNull();
        shift!.DriverId.Should().Be(_driverId);
        shift.Status.Should().Be(ShiftStatus.Active);
        shift.ClockInLat.Should().Be(-36.8485000m);
        shift.ClockInLng.Should().Be(174.7633000m);
        shift.VehicleId.Should().Be(_vehicleId);
    }

    [Fact]
    public async Task F6_1_ClockIn_DegradesToNoCoordinates_WhenLocationUnavailable()
    {
        // Arrange: 司机拒绝授权地理位置（正常路径降级为无坐标）
        var request = new ClockInRequest(
            DriverId: null,
            ClockInAt: new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12)),
            Latitude: -36.8485000m,
            Longitude: 174.7633000m,
            LocationUnavailable: true);

        // Act
        var response = await SendAuthorizedAsync(_driverToken, HttpMethod.Post, "/api/timesheets/clock-in", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var shiftId = await response.Content.ReadFromJsonAsync<Guid>();

        await using var context = _fixture.CreateDbContext();
        var shift = await context.ShiftEntries.FindAsync(shiftId);
        shift.Should().NotBeNull();
        shift!.ClockInLat.Should().BeNull();
        shift.ClockInLng.Should().BeNull();
    }

    [Fact]
    public async Task F6_1_ClockIn_WhileAlreadyActive_Returns409Conflict()
    {
        // Arrange: 先打一次上班卡
        var baseTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var firstClockIn = new ClockInRequest(ClockInAt: baseTime);
        var firstResp = await SendAuthorizedAsync(_driverToken, HttpMethod.Post, "/api/timesheets/clock-in", firstClockIn);
        firstResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: 未下班时再次打上班卡 -> 409 Conflict
        var secondClockIn = new ClockInRequest(ClockInAt: baseTime.AddMinutes(5));
        var secondResp = await SendAuthorizedAsync(_driverToken, HttpMethod.Post, "/api/timesheets/clock-in", secondClockIn);

        // Assert
        secondResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task F6_1_ClockOut_Success_SetsCompletedStatus_AndCalculatesHours()
    {
        // Arrange: 先打上班卡
        var clockInTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var clockInResp = await SendAuthorizedAsync(_driverToken, HttpMethod.Post, "/api/timesheets/clock-in",
            new ClockInRequest(ClockInAt: clockInTime));
        clockInResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: 下班打卡
        var clockOutTime = new DateTimeOffset(2026, 8, 24, 17, 0, 0, TimeSpan.FromHours(12));
        var clockOutReq = new ClockOutRequest(
            ClockOutAt: clockOutTime,
            Latitude: -36.8500000m,
            Longitude: 174.7600000m,
            BreakMinutes: 30,
            Note: "Standard shift completed");

        var clockOutResp = await SendAuthorizedAsync(_driverToken, HttpMethod.Post, "/api/timesheets/clock-out", clockOutReq);

        // Assert
        clockOutResp.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var context = _fixture.CreateDbContext();
        var completedShift = await context.ShiftEntries
            .FirstOrDefaultAsync(s => s.DriverId == _driverId && s.Status == ShiftStatus.Completed);

        completedShift.Should().NotBeNull();
        completedShift!.ClockOutAt.Should().Be(clockOutTime);
        completedShift.BreakMinutes.Should().Be(30);
        completedShift.Note.Should().Be("Standard shift completed");
    }

    #endregion

    #region AC F6.2: 跨零点班次工时与归属日

    [Fact]
    public async Task F6_2_CrossMidnightShift_Calculates8Hours_AndAttributesToClockInDate()
    {
        // Arrange (F6.2: 22:00 上班、06:00 下班 = 8 小时，归属上班日)
        var nzOffset = TimeSpan.FromHours(12);
        var clockIn = new DateTimeOffset(2026, 8, 20, 22, 0, 0, nzOffset);
        var clockOut = new DateTimeOffset(2026, 8, 21, 6, 0, 0, nzOffset);

        var shiftId = Guid.NewGuid();
        var shift = new ShiftEntry(shiftId, _driverId, clockIn, -36.8485m, 174.7633m, _vehicleId);
        shift.ClockOut(clockOut, -36.8485m, 174.7633m, breakMinutes: 0, note: "Overnight shift");

        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shift);
            await context.SaveChangesAsync();
        }

        // Act 1: 司机查询详情端点 GET /api/timesheets/{id}
        var detailResp = await SendAuthorizedAsync(_driverToken, HttpMethod.Get, $"/api/timesheets/{shiftId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResp.Content.ReadFromJsonAsync<ShiftEntryDto>();

        // Assert 1
        detail.Should().NotBeNull();
        detail!.AttributedDate.Should().Be(new DateOnly(2026, 8, 20), "跨零点班次必须归属上班日起始日");
        detail.RawDurationHours.Should().Be(8.0m);
        detail.PayableHours.Should().Be(8.0m);

        // Act 2: 司机查询汇总端点 GET /api/timesheets/me/summary?fromDate=2026-08-20&toDate=2026-08-20
        var summaryResp = await SendAuthorizedAsync(_driverToken, HttpMethod.Get,
            "/api/timesheets/me/summary?fromDate=2026-08-20&toDate=2026-08-20");
        summaryResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResp.Content.ReadFromJsonAsync<TimesheetSummaryDto>();

        // Assert 2
        summary.Should().NotBeNull();
        summary!.TotalShifts.Should().Be(1);
        summary.TotalPayableHours.Should().Be(8.0m);
        summary.TotalOrdinaryHours.Should().Be(8.0m);
        summary.DailySummaries.Should().ContainSingle(d => d.Date == new DateOnly(2026, 8, 20) && d.PayableHours == 8.0m);
    }

    #endregion

    #region AC F6.3: 夏令时切换日时长计算

    [Fact]
    public async Task F6_3_DaylightSavingTime_SpringForwardAndFallBack_CalculatesAccurateDuration()
    {
        // Arrange (F6.3: 时区固定 Pacific/Auckland，DST 切换日时长计算正确)
        // 1. 2026-09-27 春季跳变（02:00 -> 03:00，+12 -> +13，该小时跳过）
        // 01:00+12 -> 05:00+13 = 3 小时净时长
        var springIn = new DateTimeOffset(2026, 9, 27, 1, 0, 0, TimeSpan.FromHours(12));
        var springOut = new DateTimeOffset(2026, 9, 27, 5, 0, 0, TimeSpan.FromHours(13));
        var springShiftId = Guid.NewGuid();
        var springShift = new ShiftEntry(springShiftId, _driverId, springIn);
        springShift.ClockOut(springOut, breakMinutes: 0, note: "Spring forward shift");

        // 2. 2026-04-05 秋季回拨（03:00 -> 02:00，+13 -> +12，该小时重复）
        // 01:00+13 -> 05:00+12 = 5 小时净时长
        var fallIn = new DateTimeOffset(2026, 4, 5, 1, 0, 0, TimeSpan.FromHours(13));
        var fallOut = new DateTimeOffset(2026, 4, 5, 5, 0, 0, TimeSpan.FromHours(12));
        var fallShiftId = Guid.NewGuid();
        var fallShift = new ShiftEntry(fallShiftId, _driverId, fallIn);
        fallShift.ClockOut(fallOut, breakMinutes: 0, note: "Fall back shift");

        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.AddRange(springShift, fallShift);
            await context.SaveChangesAsync();
        }

        // Act & Assert 1: 春季跳变时长校验
        var springResp = await SendAuthorizedAsync(_driverToken, HttpMethod.Get, $"/api/timesheets/{springShiftId}");
        springResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var springDetail = await springResp.Content.ReadFromJsonAsync<ShiftEntryDto>();
        springDetail!.RawDurationHours.Should().Be(3.0m);
        springDetail.PayableHours.Should().Be(3.0m);
        springDetail.AttributedDate.Should().Be(new DateOnly(2026, 9, 27));

        // Act & Assert 2: 秋季回拨时长校验
        var fallResp = await SendAuthorizedAsync(_driverToken, HttpMethod.Get, $"/api/timesheets/{fallShiftId}");
        fallResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fallDetail = await fallResp.Content.ReadFromJsonAsync<ShiftEntryDto>();
        fallDetail!.RawDurationHours.Should().Be(5.0m);
        fallDetail.PayableHours.Should().Be(5.0m);
        fallDetail.AttributedDate.Should().Be(new DateOnly(2026, 4, 5));
    }

    #endregion

    #region AC F6.4: 管理员更正与审计全量留存

    [Fact]
    public async Task F6_4_AdminCorrection_RequiresReason_Returns422WhenMissing()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var baseIn = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var baseOut = new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.FromHours(12));
        var shift = new ShiftEntry(shiftId, _driverId, baseIn);
        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shift);
            await context.SaveChangesAsync();
        }

        var correctRequest = new AdminCorrectShiftRequest(
            NewClockInAt: baseIn,
            NewClockOutAt: baseOut,
            NewBreakMinutes: 30,
            Reason: ""); // Empty reason

        // Act
        var response = await SendAuthorizedAsync(_adminToken, HttpMethod.Post,
            $"/api/timesheets/{shiftId}/admin-correct", correctRequest);

        // Assert: 缺理由 422
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task F6_4_AdminCorrection_Success_PersistsChanges_AndRecordsFullAuditLog()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var origIn = new DateTimeOffset(2026, 8, 24, 8, 30, 0, TimeSpan.FromHours(12));
        var origOut = new DateTimeOffset(2026, 8, 24, 17, 0, 0, TimeSpan.FromHours(12));
        var shift = new ShiftEntry(shiftId, _driverId, origIn, -36.8485m, 174.7633m, _vehicleId);
        shift.ClockOut(origOut, breakMinutes: 30, note: "Original");

        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shift);
            await context.SaveChangesAsync();
        }

        var newIn = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var newOut = new DateTimeOffset(2026, 8, 24, 17, 30, 0, TimeSpan.FromHours(12));
        const string reason = "Driver forgot to clock in due to depot entrance gate jam";

        var correctRequest = new AdminCorrectShiftRequest(
            NewClockInAt: newIn,
            NewClockOutAt: newOut,
            NewBreakMinutes: 45,
            Reason: reason);

        // Act
        var response = await SendAuthorizedAsync(_adminToken, HttpMethod.Post,
            $"/api/timesheets/{shiftId}/admin-correct", correctRequest);

        // Assert 1: 成功返回 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert 2: 数据库班次更新
        await using var verifyContext = _fixture.CreateDbContext();
        var updatedShift = await verifyContext.ShiftEntries.FindAsync(shiftId);
        updatedShift.Should().NotBeNull();
        updatedShift!.ClockInAt.Should().Be(newIn);
        updatedShift.ClockOutAt.Should().Be(newOut);
        updatedShift.BreakMinutes.Should().Be(45);
        updatedShift.AdminCorrectionReason.Should().Be(reason);
        updatedShift.CorrectedByUserId.Should().Be(_adminUserId);

        // Assert 3: 审计日志全量留存原值与新值
        var audit = await verifyContext.AuditEvents
            .FirstOrDefaultAsync(a => a.Action == "AdminCorrectShift" && a.EntityId == shiftId.ToString());

        audit.Should().NotBeNull();
        audit!.EntityType.Should().Be("ShiftEntry");
        audit.ActorUserId.Should().Be(_adminUserId);
        audit.BeforeJson.Should().NotBeNullOrWhiteSpace();
        audit.BeforeJson.Should().Contain("\"breakMinutes\":30");
        audit.BeforeJson.Should().Contain("\"status\":\"Completed\"");
        audit.BeforeJson.Should().Contain(origIn.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        audit.AfterJson.Should().NotBeNullOrWhiteSpace();
        audit.AfterJson.Should().Contain("\"breakMinutes\":45");
        audit.AfterJson.Should().Contain(reason);
        audit.AfterJson.Should().Contain(newIn.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
    }

    #endregion

    #region AC F6.5: 工时汇总两端数字完全一致（误差 0）

    /// <summary>
    /// F6.5 核心验收标准：
    /// 司机端与管理端必须走同一套聚合逻辑，使用同一批种子数据分别调两个端点，断言数字完全相等。
    /// 行号标记供任务书完成标准报告引用。
    /// </summary>
    [Fact]
    public async Task F6_5_TimesheetSummary_DriverAndAdminEndpoints_ReturnExactIdenticalNumbers_ZeroDiscrepancy()
    {
        // Arrange: 注入本测试独占的确定性班次数据集（包含标准班次、加班班次与跨零点班次）
        var nzOffset = TimeSpan.FromHours(12);

        // 班次 1: 2026-08-10 08:00 - 16:30 (休息 30 分钟 -> 8.0h 常规工时)
        var shift1 = new ShiftEntry(Guid.NewGuid(), _driverId, new DateTimeOffset(2026, 8, 10, 8, 0, 0, nzOffset), -36.8485m, 174.7633m, _vehicleId);
        shift1.ClockOut(new DateTimeOffset(2026, 8, 10, 16, 30, 0, nzOffset), breakMinutes: 30, note: "Standard shift");

        // 班次 2: 2026-08-11 07:00 - 18:00 (休息 60 分钟 -> 8.0h 常规 + 2.0h 加班)
        var shift2 = new ShiftEntry(Guid.NewGuid(), _driverId, new DateTimeOffset(2026, 8, 11, 7, 0, 0, nzOffset), -36.8485m, 174.7633m, _vehicleId);
        shift2.ClockOut(new DateTimeOffset(2026, 8, 11, 18, 0, 0, nzOffset), breakMinutes: 60, note: "Overtime shift");

        // 班次 3: 2026-08-12 22:00 - 2026-08-13 06:00 (跨零点夜班 -> 8.0h 常规，归属于 2026-08-12)
        var shift3 = new ShiftEntry(Guid.NewGuid(), _driverId, new DateTimeOffset(2026, 8, 12, 22, 0, 0, nzOffset), -36.8485m, 174.7633m, _vehicleId);
        shift3.ClockOut(new DateTimeOffset(2026, 8, 13, 6, 0, 0, nzOffset), breakMinutes: 0, note: "Cross midnight shift");

        // 班次 4: 2026-08-13 08:00 进行中未下班活跃班次（不应计入完成汇总）
        var shift4 = new ShiftEntry(Guid.NewGuid(), _driverId, new DateTimeOffset(2026, 8, 13, 8, 0, 0, nzOffset), -36.8485m, 174.7633m, _vehicleId);

        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.AddRange(shift1, shift2, shift3, shift4);
            await context.SaveChangesAsync();
        }

        const string fromDateStr = "2026-08-10";
        const string toDateStr = "2026-08-15";

        // Act 1: 司机端调用本期工时汇总端点 GET /api/timesheets/me/summary
        var driverUrl = $"/api/timesheets/me/summary?fromDate={fromDateStr}&toDate={toDateStr}";
        var driverResponse = await SendAuthorizedAsync(_driverToken, HttpMethod.Get, driverUrl);
        driverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var driverSummary = await driverResponse.Content.ReadFromJsonAsync<TimesheetSummaryDto>();

        // Act 2: 管理端调用汇总端点 GET /api/timesheets/summary?driverId=...
        var adminUrl = $"/api/timesheets/summary?driverId={_driverId}&fromDate={fromDateStr}&toDate={toDateStr}";
        var adminResponse = await SendAuthorizedAsync(_adminToken, HttpMethod.Get, adminUrl);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminSummary = await adminResponse.Content.ReadFromJsonAsync<TimesheetSummaryDto>();

        // Assert: 两端返回数据完全一致，误差 0
        driverSummary.Should().NotBeNull();
        adminSummary.Should().NotBeNull();

        driverSummary!.TotalShifts.Should().Be(3);
        driverSummary.TotalPayableHours.Should().Be(26.0m);
        driverSummary.TotalOrdinaryHours.Should().Be(24.0m);
        driverSummary.TotalOvertimeHours.Should().Be(2.0m);
        driverSummary.TotalBreakMinutes.Should().Be(90);

        driverSummary.TotalShifts.Should().Be(adminSummary!.TotalShifts, "总班次数两端必须完全相等");
        driverSummary.TotalPayableHours.Should().Be(adminSummary.TotalPayableHours, "计薪总工时两端必须完全相等（误差 0）");
        driverSummary.TotalOrdinaryHours.Should().Be(adminSummary.TotalOrdinaryHours, "常规工时两端必须完全相等（误差 0）");
        driverSummary.TotalOvertimeHours.Should().Be(adminSummary.TotalOvertimeHours, "加班工时两端必须完全相等（误差 0）");
        driverSummary.TotalBreakMinutes.Should().Be(adminSummary.TotalBreakMinutes, "休息分钟数两端必须完全相等");

        driverSummary.DailySummaries.Should().HaveSameCount(adminSummary.DailySummaries, "每日聚合明细条数两端必须一致");

        for (var i = 0; i < driverSummary.DailySummaries.Count; i++)
        {
            var dItem = driverSummary.DailySummaries[i];
            var aItem = adminSummary.DailySummaries[i];

            dItem.Date.Should().Be(aItem.Date);
            dItem.ShiftCount.Should().Be(aItem.ShiftCount);
            dItem.PayableHours.Should().Be(aItem.PayableHours);
            dItem.OrdinaryHours.Should().Be(aItem.OrdinaryHours);
            dItem.OvertimeHours.Should().Be(aItem.OvertimeHours);
            dItem.BreakMinutes.Should().Be(aItem.BreakMinutes);
        }
    }

    #endregion

    #region IDOR Negative Tests: 越权访问全部返回 403 Forbidden

    [Fact]
    public async Task F6_IDOR_Driver_QueryingOtherDriverSummary_Returns403Forbidden()
    {
        // Act: 司机 Dave 尝试查询司机 Bob 的工时汇总
        var response = await SendAuthorizedAsync(_driverToken, HttpMethod.Get,
            $"/api/timesheets/summary?driverId={_otherDriverId}");

        // Assert: 必须返回 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task F6_IDOR_Driver_QueryingOtherDriverTimesheetsList_Returns403Forbidden()
    {
        // Act: 司机 Dave 尝试查询司机 Bob 的打卡列表
        var response = await SendAuthorizedAsync(_driverToken, HttpMethod.Get,
            $"/api/timesheets?driverId={_otherDriverId}");

        // Assert: 必须返回 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task F6_IDOR_Driver_QueryingOtherDriverShiftById_Returns403Forbidden()
    {
        // Arrange: 为司机 Bob 创建一条班次
        var otherShiftId = Guid.NewGuid();
        var shiftTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var shift = new ShiftEntry(otherShiftId, _otherDriverId, shiftTime);
        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shift);
            await context.SaveChangesAsync();
        }

        // Act: 司机 Dave 尝试按 ID 获取司机 Bob 的班次记录
        var response = await SendAuthorizedAsync(_driverToken, HttpMethod.Get,
            $"/api/timesheets/{otherShiftId}");

        // Assert: 必须返回 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task F6_IDOR_Driver_QueryingOtherDriverActiveShift_Returns403Forbidden()
    {
        // Act: 司机 Dave 尝试查询司机 Bob 的活跃班次
        var response = await SendAuthorizedAsync(_driverToken, HttpMethod.Get,
            $"/api/timesheets/active?driverId={_otherDriverId}");

        // Assert: 必须返回 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task F6_IDOR_Driver_ClockingInForOtherDriver_Returns403Forbidden()
    {
        // Act: 司机 Dave 尝试替司机 Bob 打上班卡
        var req = new ClockInRequest(
            DriverId: _otherDriverId,
            ClockInAt: new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12)));
        var response = await SendAuthorizedAsync(_driverToken, HttpMethod.Post,
            "/api/timesheets/clock-in", req);

        // Assert: 必须返回 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task F6_IDOR_Driver_ClockingOutOtherDriverShift_Returns403Forbidden()
    {
        // Arrange: 司机 Bob 的活跃班次
        var otherShiftId = Guid.NewGuid();
        var shiftTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var shift = new ShiftEntry(otherShiftId, _otherDriverId, shiftTime);
        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shift);
            await context.SaveChangesAsync();
        }

        // Act: 司机 Dave 尝试为司机 Bob 的班次打下班卡
        var req = new ClockOutRequest(
            ShiftId: otherShiftId,
            ClockOutAt: new DateTimeOffset(2026, 8, 24, 17, 0, 0, TimeSpan.FromHours(12)));
        var response = await SendAuthorizedAsync(_driverToken, HttpMethod.Post,
            "/api/timesheets/clock-out", req);

        // Assert: 必须返回 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task F6_IDOR_Driver_CallingAdminCorrection_Returns403Forbidden()
    {
        // Arrange: 一条班次记录
        var shiftId = Guid.NewGuid();
        var shiftTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var shift = new ShiftEntry(shiftId, _driverId, shiftTime);
        await using (var context = _fixture.CreateDbContext())
        {
            context.ShiftEntries.Add(shift);
            await context.SaveChangesAsync();
        }

        // Act: 司机 Dave 尝试调用管理员更正端点
        var req = new AdminCorrectShiftRequest(
            NewClockInAt: new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12)),
            NewClockOutAt: new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.FromHours(12)),
            NewBreakMinutes: 30,
            Reason: "Driver trying to tamper with shift");

        var response = await SendAuthorizedAsync(_driverToken, HttpMethod.Post,
            $"/api/timesheets/{shiftId}/admin-correct", req);

        // Assert: 必须返回 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

public sealed class TimesheetTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TimesheetTestWebApplicationFactory(string connectionString)
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
