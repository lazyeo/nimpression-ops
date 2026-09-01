using System.IO.Compression;
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
using Nimpression.Application.Features.Privacy.DTOs;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Security;
using Nimpression.Integration.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace Nimpression.Integration.Tests.Privacy;

[Collection("PostgreSqlCollection")]
public sealed class PrivacyIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PasswordHasher _passwordHasher = new();
    private PrivacyTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _driverAUserId = Guid.NewGuid();
    private readonly Guid _driverADriverId = Guid.NewGuid();
    private readonly Guid _driverBUserId = Guid.NewGuid();
    private readonly Guid _driverBDriverId = Guid.NewGuid();
    private readonly Guid _vehicleId = Guid.NewGuid();

    private readonly string _adminEmail = TestDataFactory.CreateEmail("priv_admin");
    private readonly string _driverAEmail = TestDataFactory.CreateEmail("priv_driver_a");
    private readonly string _driverBEmail = TestDataFactory.CreateEmail("priv_driver_b");
    private const string DefaultPassword = "Password123!";

    private string _adminToken = string.Empty;
    private string _driverAToken = string.Empty;
    private string _driverBToken = string.Empty;

    public PrivacyIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new PrivacyTestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();

        var adminUser = new User(_adminUserId, new EmailAddress(_adminEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Admin, "Admin Privacy");
        var driverAUser = new User(_driverAUserId, new EmailAddress(_driverAEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Alice Driver");
        var driverBUser = new User(_driverBUserId, new EmailAddress(_driverBEmail), _passwordHasher.HashPassword(DefaultPassword), UserRole.Driver, "Bob Driver");

        var driverA = new Driver(
            _driverADriverId,
            _driverAUserId,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(36m),
            new Money(46m),
            new Money(0.88m),
            "+64 21 111 2222",
            "100 Queen St, Auckland Central",
            "Sarah Connor (+64 21 333 4444)",
            new DateOnly(2024, 6, 1),
            DriverStatus.Active);

        var driverB = new Driver(
            _driverBDriverId,
            _driverBUserId,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 2",
            new DateOnly(2028, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.80m),
            "+64 22 555 6666",
            "200 Karangahape Rd, Auckland",
            "John Connor (+64 22 777 8888)",
            new DateOnly(2024, 6, 1),
            DriverStatus.Active);

        var vehicle = new Vehicle(
            _vehicleId,
            TestDataFactory.CreateRegoObject("P"),
            "Isuzu",
            "Forward",
            2024,
            "7NZVPRIVACYVIN123",
            new Kilometres(15000),
            new Kilometres(10000));

        context.Users.AddRange(adminUser, driverAUser, driverBUser);
        context.Drivers.AddRange(driverA, driverB);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        _adminToken = CreateAccessToken(_adminUserId, _adminEmail, UserRole.Admin, "Admin Privacy");
        _driverAToken = CreateAccessToken(_driverAUserId, _driverAEmail, UserRole.Driver, "Alice Driver");
        _driverBToken = CreateAccessToken(_driverBUserId, _driverBEmail, UserRole.Driver, "Bob Driver");
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
            var testUserIds = new[] { _adminUserId, _driverAUserId, _driverBUserId };

            var shifts = await context.ShiftEntries.Where(s => testDriverIds.Contains(s.DriverId)).ToListAsync();
            context.ShiftEntries.RemoveRange(shifts);

            var payslips = await context.Payslips.Include(p => p.Lines).Where(p => testDriverIds.Contains(p.DriverId)).ToListAsync();
            context.Payslips.RemoveRange(payslips);

            var incidents = await context.IncidentReports.Where(i => testDriverIds.Contains(i.DriverId)).ToListAsync();
            context.IncidentReports.RemoveRange(incidents);

            var dsrs = await context.DataSubjectRequests.Where(d => testUserIds.Contains(d.SubjectUserId)).ToListAsync();
            context.DataSubjectRequests.RemoveRange(dsrs);

            var vehicles = await context.Vehicles.Where(v => v.Id == _vehicleId).ToListAsync();
            context.Vehicles.RemoveRange(vehicles);

            var drivers = await context.Drivers.Where(d => testDriverIds.Contains(d.Id)).ToListAsync();
            context.Drivers.RemoveRange(drivers);

            var users = await context.Users.Where(u => testUserIds.Contains(u.Id)).ToListAsync();
            context.Users.RemoveRange(users);

            await context.SaveChangesAsync();
        }
        catch
        {
            // Ignore cleanup exceptions
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    #region N2.1: PII 加密（psql 直查原表全为密文）

    [Fact]
    public async Task N2_1_Driver_And_Vehicle_PII_Stored_As_Ciphertext_In_Database_And_Decrypted_Via_EF()
    {
        // 1. 原生 SQL 直查 Drivers 表（模拟 psql 命令行）
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using (var cmd = new NpgsqlCommand("SELECT \"PhoneEnc\", \"AddressEnc\", \"EmergencyContactEnc\" FROM \"Drivers\" WHERE \"Id\" = @id", connection))
        {
            cmd.Parameters.AddWithValue("id", _driverADriverId);
            await using var reader = await cmd.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();

            var phoneRaw = reader.GetString(0);
            var addressRaw = reader.GetString(1);
            var emgRaw = reader.GetString(2);

            // 断言：原生 SQL / psql 直查结果全部带 "enc:v1:" 前缀，且绝不包含明文字符串
            phoneRaw.Should().StartWith("enc:v1:", "手机号必须在数据库中以显式版本密文存储");
            phoneRaw.Should().NotContain("+64 21 111 2222", "psql 直查绝对看不到明文手机号");

            addressRaw.Should().StartWith("enc:v1:", "地址必须在数据库中以显式版本密文存储");
            addressRaw.Should().NotContain("100 Queen St", "psql 直查绝对看不到明文住址");

            emgRaw.Should().StartWith("enc:v1:", "紧急联系人必须在数据库中以显式版本密文存储");
            emgRaw.Should().NotContain("Sarah Connor", "psql 直查绝对看不到明文紧急联系人姓名");
        }

        // 2. 原生 SQL 直查 Vehicles 表中的 VIN
        await using (var cmd = new NpgsqlCommand("SELECT \"VinEnc\" FROM \"Vehicles\" WHERE \"Id\" = @id", connection))
        {
            cmd.Parameters.AddWithValue("id", _vehicleId);
            var vinRaw = (string?)(await cmd.ExecuteScalarAsync());

            vinRaw.Should().NotBeNullOrWhiteSpace();
            vinRaw.Should().StartWith("enc:v1:", "车辆 VIN 码在数据库中必须为密文");
            vinRaw.Should().NotContain("7NZVPRIVACYVIN123", "psql 直查绝对看不到明文 VIN 码");
        }

        // 3. EF Core 实体读取自动解密为明文
        await using var efContext = _fixture.CreateDbContext();
        var efDriver = await efContext.Drivers.FindAsync(_driverADriverId);
        efDriver.Should().NotBeNull();
        efDriver!.PhoneEnc.Should().Be("+64 21 111 2222", "EF Core 实体读取时应由 ValueConverter 自动解密回原始明文");
        efDriver.AddressEnc.Should().Be("100 Queen St, Auckland Central");
        efDriver.EmergencyContactEnc.Should().Be("Sarah Connor (+64 21 333 4444)");

        var efVehicle = await efContext.Vehicles.FindAsync(_vehicleId);
        efVehicle.Should().NotBeNull();
        efVehicle!.VinEnc.Should().Be("7NZVPRIVACYVIN123", "EF Core 实体读取车辆 VIN 时自动解密回明文");
    }

    #endregion

    #region N2.2: 数据分级清单端点

    [Fact]
    public async Task N2_2_DataClassification_Endpoint_Returns_Full_Asset_Inventory()
    {
        var response = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, "/api/privacy/classification");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<DataClassificationDto>>();
        list.Should().NotBeNullOrEmpty();
        list.Should().Contain(x => x.EntityName == "Driver" && x.FieldName == "PhoneEnc" && x.IsEncryptedAtRest);
        list.Should().Contain(x => x.EntityName == "Vehicle" && x.FieldName == "VinEnc" && x.IsEncryptedAtRest);
        list.Should().Contain(x => x.EntityName == "ShiftEntry" && x.RetentionPeriod.Contains("90 days"));
    }

    #endregion

    #region N2.3: 保留策略（默认 dry-run 与执行报告）

    [Fact]
    public async Task N2_3_RetentionPolicy_Cleanup_Defaults_To_DryRun_And_Performs_Real_Purge_When_Explicit()
    {
        var baseDate = new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);
        var oldShiftDate = baseDate.AddDays(-100); // 100 天前（超过 90 天保存期）
        var recentShiftDate = baseDate.AddDays(-10); // 10 天前（在保存期内）

        var oldShift = new ShiftEntry(Guid.NewGuid(), _driverADriverId, oldShiftDate, -36.8485m, 174.7633m);
        oldShift.ClockOut(oldShiftDate.AddHours(8), -36.8500m, 174.7600m, 30, "Old shift");

        var recentShift = new ShiftEntry(Guid.NewGuid(), _driverADriverId, recentShiftDate, -36.8400m, 174.7600m);
        recentShift.ClockOut(recentShiftDate.AddHours(8), -36.8420m, 174.7620m, 30, "Recent shift");

        await using (var seedContext = _fixture.CreateDbContext())
        {
            seedContext.ShiftEntries.AddRange(oldShift, recentShift);
            await seedContext.SaveChangesAsync();
        }

        // 1. 默认执行（不传参数 / 默认 dry-run）
        var dryRunReq = new ExecuteRetentionCleanupRequest(ReferenceDate: baseDate, Execute: false);
        var dryRunResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/privacy/cleanup", dryRunReq);
        dryRunResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var dryRunReport = await dryRunResp.Content.ReadFromJsonAsync<RetentionCleanupReportDto>();
        dryRunReport.Should().NotBeNull();
        dryRunReport!.IsDryRun.Should().BeTrue("AC N2.3: 默认必须为 dry-run 模式");
        dryRunReport.ShiftGpsCoordinatesPurgedCount.Should().BeGreaterThanOrEqualTo(1);

        // 验证数据库中老班次的 GPS 坐标在 dry-run 后仍未被删除
        await using (var verifyContext = _fixture.CreateDbContext())
        {
            var dbOldShift = await verifyContext.ShiftEntries.FindAsync(oldShift.Id);
            dbOldShift.Should().NotBeNull();
            dbOldShift!.ClockInLat.Should().NotBeNull("Dry-Run 模式下数据库坐标绝不应被清空");
            dbOldShift.ClockInLng.Should().NotBeNull();
        }

        // 2. 显式传入 Execute = true 执行真删/脱敏
        var liveReq = new ExecuteRetentionCleanupRequest(ReferenceDate: baseDate, Execute: true);
        var liveResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, "/api/privacy/cleanup", liveReq);
        liveResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var liveReport = await liveResp.Content.ReadFromJsonAsync<RetentionCleanupReportDto>();
        liveReport.Should().NotBeNull();
        liveReport!.IsDryRun.Should().BeFalse();

        // 验证数据库：老班次的 GPS 坐标被设置为 null，但工时信息与上下班时间完整保留！
        await using (var verifyContext = _fixture.CreateDbContext())
        {
            var dbOldShift = await verifyContext.ShiftEntries.FindAsync(oldShift.Id);
            dbOldShift.Should().NotBeNull();
            dbOldShift!.ClockInLat.Should().BeNull("90天前班次的打卡 GPS 经度应被脱敏置空");
            dbOldShift.ClockInLng.Should().BeNull();
            dbOldShift.ClockOutLat.Should().BeNull();
            dbOldShift.ClockOutLng.Should().BeNull();
            dbOldShift.ClockInAt.Should().Be(oldShiftDate, "班次上下班时间戳必须完整保留用于法定工时与薪资审计");
            dbOldShift.CalculateWorkHours().Value.Should().Be(7.5m, "工时计算必须完全不受 GPS 清理影响");

            // 10天前的新班次坐标必须依然存在
            var dbRecentShift = await verifyContext.ShiftEntries.FindAsync(recentShift.Id);
            dbRecentShift.Should().NotBeNull();
            dbRecentShift!.ClockInLat.Should().NotBeNull("90天内的活跃打卡坐标必须保留");
        }
    }

    #endregion

    #region N2.4: 个人数据导出（IPP 6 查阅权）与越权拦截

    [Fact]
    public async Task N2_4_Driver_Can_Export_Own_Personal_Data_Zip_And_IDOR_Blocked_With_403()
    {
        var area = new Area(Guid.NewGuid(), "Auckland CBD", "AKL-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant());
        var shift = new ShiftEntry(Guid.NewGuid(), _driverADriverId, new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.FromHours(12)));
        shift.ClockOut(new DateTimeOffset(2026, 8, 20, 16, 30, 0, TimeSpan.FromHours(12)), breakMinutes: 30);

        var taskRef = "TSK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var task = new JobTask(Guid.NewGuid(), taskRef, "Port to Depot Delivery", area.Id, new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.FromHours(12)), _adminUserId, driverId: _driverADriverId, vehicleId: _vehicleId);

        var incident = new IncidentReport(Guid.NewGuid(), _driverADriverId, _vehicleId, new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(12)), "Ponsonby Rd", IncidentSeverity.Minor, "Minor bumper scratch");

        await using (var context = _fixture.CreateDbContext())
        {
            context.Areas.Add(area);
            context.ShiftEntries.Add(shift);
            context.JobTasks.Add(task);
            context.IncidentReports.Add(incident);
            await context.SaveChangesAsync();
        }

        // 2. 司机 A 导出本人数据（自助导出）
        var exportResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, "/api/privacy/export");
        exportResp.StatusCode.Should().Be(HttpStatusCode.OK);
        exportResp.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");

        var zipBytes = await exportResp.Content.ReadAsByteArrayAsync();
        zipBytes.Should().NotBeEmpty();

        using var mem = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(mem, ZipArchiveMode.Read);
        archive.Entries.Should().Contain(e => e.FullName == "driver_data_export.json");
        archive.Entries.Should().Contain(e => e.FullName == "README.txt");

        var jsonEntry = archive.GetEntry("driver_data_export.json")!;
        using var jsonStream = jsonEntry.Open();
        using var doc = await JsonDocument.ParseAsync(jsonStream);

        doc.RootElement.GetProperty("user").GetProperty("email").GetString().Should().Be(_driverAEmail);
        doc.RootElement.GetProperty("driver").GetProperty("phone").GetString().Should().Be("+64 21 111 2222", "导出的数据中包含明文以便司机查阅本人数据");
        doc.RootElement.GetProperty("shifts").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        // 3. IDOR 拦截：司机 A 尝试导出司机 B 的个人数据
        var idorResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, $"/api/privacy/export/{_driverBUserId}");
        idorResp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "硬约束：导出的 zip 不许包含他人数据，司机越权必须返回 403 而非 404");
    }

    #endregion

    #region N2.5: 离职司机数据匿名化（SUM(GrossPay)、审计条数、事故条数完全不变）

    [Fact]
    public async Task N2_5_Driver_Anonymization_Replaces_PII_With_Placeholders_And_Preserves_Financial_And_Statutory_Invariants()
    {
        var payPeriod1 = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 14), PayPeriodStatus.Finalised);
        var payPeriod2 = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 28), PayPeriodStatus.Finalised);

        var payslip1 = new Payslip(
            Guid.NewGuid(),
            payPeriod1.Id,
            _driverADriverId,
            new WorkHours(40m),
            new WorkHours(0m),
            new WorkHours(0m),
            new Money(36m),
            new Money(1440m),
            10,
            new Kilometres(300),
            new Money(46m),
            new Money(0.88m),
            new Money(724m),
            PayBasis.Hourly,
            new Money(1440m),
            false,
            new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero));

        var payslip2 = new Payslip(
            Guid.NewGuid(),
            payPeriod2.Id,
            _driverADriverId,
            new WorkHours(45m),
            new WorkHours(5m),
            new WorkHours(0m),
            new Money(36m),
            new Money(1800m),
            12,
            new Kilometres(350),
            new Money(46m),
            new Money(0.88m),
            new Money(860m),
            PayBasis.Hourly,
            new Money(1800m),
            false,
            new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        var incident = new IncidentReport(
            Guid.NewGuid(),
            _driverADriverId,
            _vehicleId,
            new DateTimeOffset(2026, 8, 10, 15, 0, 0, TimeSpan.Zero),
            "Penrose Depot",
            IncidentSeverity.Minor,
            "Loading ramp contact");

        await using (var context = _fixture.CreateDbContext())
        {
            context.PayPeriods.AddRange(payPeriod1, payPeriod2);
            context.Payslips.AddRange(payslip1, payslip2);
            context.IncidentReports.Add(incident);
            await context.SaveChangesAsync();
        }

        // 匿名化前：通过原生 SQL 查询基准数值
        decimal grossPaySumBefore;
        int payslipCountBefore;
        int incidentCountBefore;

        await using (var connection = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await connection.OpenAsync();

            await using (var cmd = new NpgsqlCommand("SELECT COALESCE(SUM(\"GrossPayAmount\"), 0) FROM \"Payslips\" WHERE \"DriverId\" = @driverId", connection))
            {
                cmd.Parameters.AddWithValue("driverId", _driverADriverId);
                grossPaySumBefore = (decimal)(await cmd.ExecuteScalarAsync())!;
            }

            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Payslips\" WHERE \"DriverId\" = @driverId", connection))
            {
                cmd.Parameters.AddWithValue("driverId", _driverADriverId);
                payslipCountBefore = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            }

            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"IncidentReports\" WHERE \"DriverId\" = @driverId", connection))
            {
                cmd.Parameters.AddWithValue("driverId", _driverADriverId);
                incidentCountBefore = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        grossPaySumBefore.Should().Be(3240m); // 1440 + 1800
        payslipCountBefore.Should().Be(2);
        incidentCountBefore.Should().Be(1);

        // Act: 管理员执行匿名化
        var anonResp = await SendAuthorizedAsync(_adminToken, HttpMethod.Post, $"/api/privacy/anonymize/{_driverADriverId}", new AnonymizeDriverRequest("Driver resigned"));
        anonResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var anonResult = await anonResp.Content.ReadFromJsonAsync<AnonymizationResultDto>();

        anonResult.Should().NotBeNull();
        anonResult!.GrossPaySumBefore.Should().Be(grossPaySumBefore);
        anonResult.GrossPaySumAfter.Should().Be(grossPaySumBefore);
        anonResult.PayslipsCountBefore.Should().Be(payslipCountBefore);
        anonResult.PayslipsCountAfter.Should().Be(payslipCountBefore);

        // Assert: 匿名化后原生 SQL 查询对比 —— 关键纪律 N2.5：断言匿名化前后 SUM(GrossPay)、审计条数、事故条数完全不变！
        await using (var connection = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await connection.OpenAsync();

            await using (var cmd = new NpgsqlCommand("SELECT COALESCE(SUM(\"GrossPayAmount\"), 0) FROM \"Payslips\" WHERE \"DriverId\" = @driverId", connection))
            {
                cmd.Parameters.AddWithValue("driverId", _driverADriverId);
                var grossPaySumAfter = (decimal)(await cmd.ExecuteScalarAsync())!;
                grossPaySumAfter.Should().Be(grossPaySumBefore, "匿名化后历史工资单总额 SUM(GrossPay) 必须分毫不差");
            }

            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Payslips\" WHERE \"DriverId\" = @driverId", connection))
            {
                cmd.Parameters.AddWithValue("driverId", _driverADriverId);
                var payslipCountAfter = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
                payslipCountAfter.Should().Be(payslipCountBefore, "匿名化后工资单记录数必须完全不变");
            }

            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"IncidentReports\" WHERE \"DriverId\" = @driverId", connection))
            {
                cmd.Parameters.AddWithValue("driverId", _driverADriverId);
                var incidentCountAfter = Convert.ToInt32(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
                incidentCountAfter.Should().Be(incidentCountBefore, "匿名化后事故记录数必须完全不变以供保险追偿与法定审计");
            }

            // 验证 Drivers 与 Users 表中的 PII 已被替换为不可逆占位符
            await using (var cmd = new NpgsqlCommand("SELECT \"DisplayName\", \"Email\", \"Status\" FROM \"Users\" WHERE \"Id\" = @userId", connection))
            {
                cmd.Parameters.AddWithValue("userId", _driverAUserId);
                await using var reader = await cmd.ExecuteReaderAsync();
                (await reader.ReadAsync()).Should().BeTrue();

                var displayName = reader.GetString(0);
                var email = reader.GetString(1);
                var status = reader.GetString(2);

                displayName.Should().StartWith("Driver #", "用户显示名称应替换为不可逆占位符如 Driver #a1b2c3");
                displayName.Should().NotContain("Alice", "真实姓名明文必须被替换");
                email.Should().EndWith("@privacy.internal", "邮箱应替换为不可逆内部占位邮箱");
                email.Should().NotBe(_driverAEmail);
                status.Should().Be("Inactive", "匿名化后账号状态必须转为 Inactive");
            }
        }
    }

    #endregion

    #region N2.7: 同意与告知（首次登录展示隐私声明，记录同意版本与时间戳）

    [Fact]
    public async Task N2_7_Consent_Recording_And_Retrieval_Works_Across_Versions()
    {
        // 1. 查询当前同意状态（初始状态未同意）
        var getResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, "/api/privacy/consent?version=2026.1");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var consentStatus = await getResp.Content.ReadFromJsonAsync<PrivacyConsentDto>();

        consentStatus.Should().NotBeNull();
        consentStatus!.PolicyVersion.Should().Be("2026.1");
        consentStatus.HasConsented.Should().BeFalse("未提交前同意状态应为 false");
        consentStatus.ContentMarkdown.Should().Contain("Privacy Act 2020");

        // 2. 提交同意记录
        var postResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Post, "/api/privacy/consent", new RecordConsentRequest("2026.1"));
        postResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var recordedConsent = await postResp.Content.ReadFromJsonAsync<PrivacyConsentDto>();
        recordedConsent.Should().NotBeNull();
        recordedConsent!.HasConsented.Should().BeTrue();
        recordedConsent.ConsentedAt.Should().NotBeNull();

        // 3. 再次查询确认持久化成功
        var verifyResp = await SendAuthorizedAsync(_driverAToken, HttpMethod.Get, "/api/privacy/consent?version=2026.1");
        verifyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var verified = await verifyResp.Content.ReadFromJsonAsync<PrivacyConsentDto>();

        verified.Should().NotBeNull();
        verified!.HasConsented.Should().BeTrue();
        verified.ConsentedAt.Should().NotBeNull();
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

public sealed class PrivacyTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public PrivacyTestWebApplicationFactory(string connectionString)
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
