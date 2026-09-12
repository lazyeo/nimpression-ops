using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Security;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services.Payroll;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Notifications.Fixtures;
using Npgsql;

namespace Nimpression.Integration.Tests.Payroll;

[Collection("PostgreSqlCollection")]
public sealed class DriverTaxProfileIntegrationTests(PostgreSqlContainerFixture fixture) : IAsyncLifetime
{
    private const string Mine = "/api/payroll/tax-profiles/mine";
    private const string Profiles = "/api/payroll/tax-profiles";
    // UTC is still September 11, but NZ has crossed into September 12.
    private readonly TestDateTimeProvider _clock = new(new(2026, 9, 11, 12, 30, 0, TimeSpan.Zero), new(2026, 9, 12));
    private readonly Actor _owner = Actor.Create(UserRole.Driver);
    private readonly Actor _other = Actor.Create(UserRole.Driver);
    private readonly Actor _admin = Actor.Create(UserRole.Admin);
    private readonly Actor _dispatcher = Actor.Create(UserRole.Dispatcher);
    private readonly Dictionary<Guid, string> _tokens = [];
    private DbContextOptions<AppDbContext> _options = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = $"tax_profiles_{Guid.NewGuid():N}" };
        _options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using (var context = new AppDbContext(_options))
        {
            await context.Database.MigrateAsync();
            foreach (var actor in new[] { _owner, _other, _admin, _dispatcher })
            {
                context.Users.Add(new User(actor.UserId, new EmailAddress(actor.Email), "test-only-hash", actor.Role, "Tax profile test", "en-NZ", _clock.UtcNow));
                if (actor.DriverId.HasValue)
                    context.Drivers.Add(new Driver(actor.DriverId.Value, actor.UserId, TestDataFactory.CreateEmployeeNo(),
                        "Class 2", new(2028, 1, 1), new Money(30), new Money(40), new Money(1),
                        "ENC(test phone)", "ENC(test address)", "ENC(test emergency contact)", new(2025, 1, 1)));
            }
            await context.SaveChangesAsync();
        }
        _factory = new PayrollTestWebApplicationFactory(connection.ConnectionString).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IDateTimeProvider>();
                services.AddSingleton<IDateTimeProvider>(_clock);
                // Keep signed-token authentication, but validate its lifetime against the same deterministic clock.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, _) =>
                        (!notBefore.HasValue || notBefore.Value <= _clock.UtcNow.UtcDateTime) &&
                        expires.HasValue && expires.Value > _clock.UtcNow.UtcDateTime);
            }));
        _client = _factory.CreateClient();
        var tokens = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        foreach (var actor in new[] { _owner, _other, _admin, _dispatcher })
            _tokens[actor.UserId] = tokens.GenerateAccessToken(actor.UserId, actor.Email, actor.Role.ToString(), "Tax profile test").Token;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        await using var context = new AppDbContext(_options);
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task OwnerOnlySubmissionAndHistory_DoNotExposeOtherDriversOrEmployerSettings()
    {
        var profile = await SubmitAsync(_owner, new(2026, 9, 12));
        using var own = await SendAsync(_owner, HttpMethod.Get, Mine);
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        own.Headers.CacheControl?.NoStore.Should().BeTrue();
        var ownJson = await JsonAsync(own);
        ownJson.GetProperty("totalCount").GetInt32().Should().Be(1);
        var item = ownJson.GetProperty("items")[0];
        item.GetProperty("id").GetGuid().Should().Be(profile);
        item.GetProperty("driverId").GetGuid().Should().Be(_owner.DriverId!.Value);
        item.GetProperty("status").GetString().Should().Be("Pending");
        item.GetProperty("approvedEmployee").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("declaration").GetProperty("employee").TryGetProperty("kiwiSaverEmployer", out _).Should().BeFalse();
        using var other = await SendAsync(_other, HttpMethod.Get, Mine);
        (await JsonAsync(other)).GetProperty("totalCount").GetInt32().Should().Be(0);
        using var withdraw = await SendAsync(_other, HttpMethod.Post, $"{Profiles}/{profile}/withdraw", new { });
        withdraw.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        foreach (var (actor, url) in new[] { (_dispatcher, Mine), (_dispatcher, Profiles), (_owner, Profiles) })
        {
            using var forbidden = await SendAsync(actor, HttpMethod.Get, url);
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        using var forbiddenApproval = await SendAsync(_owner, HttpMethod.Post, $"{Profiles}/{profile}/approve", Approval(new(2026, 9, 12)));
        forbiddenApproval.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SubmissionRequiresConfirmedPersonalInputs_AndUsesNzToday()
    {
        using var unconfirmed = await SendAsync(_owner, HttpMethod.Post, Mine, Submission(new(2026, 9, 12), confirmed: false));
        await AssertProblemAsync(unconfirmed, "tax_profile_confirmation_required");
        using var yesterday = await SendAsync(_owner, HttpMethod.Post, Mine, Submission(new(2026, 9, 11)));
        await AssertProblemAsync(yesterday, "tax_profile_effective_date_invalid");
        using var unconfigured = await SendAsync(_owner, HttpMethod.Post, Mine, new
        {
            effectiveFrom = "2026-09-12", confirmed = true,
            declaration = new { workerType = "Employee", employee = new { taxCode = (string?)null, kiwiSaverEmployee = (object?)null }, contractor = (object?)null }
        });
        unconfigured.IsSuccessStatusCode.Should().BeFalse();
        using var history = await SendAsync(_owner, HttpMethod.Get, Mine);
        history.StatusCode.Should().Be(HttpStatusCode.OK, await history.Content.ReadAsStringAsync());
        (await JsonAsync(history)).GetProperty("totalCount").GetInt32().Should().Be(0);
        await SubmitAsync(_owner, new(2026, 9, 12));
    }

    [Fact]
    public async Task ConcurrentSubmissions_CreateExactlyOnePendingVersion()
    {
        var responses = await Task.WhenAll(
            SendAsync(_owner, HttpMethod.Post, Mine, Submission(new(2026, 9, 12))),
            SendAsync(_owner, HttpMethod.Post, Mine, Submission(new(2026, 9, 13))));
        try
        {
            responses.Count(response => response.IsSuccessStatusCode).Should().Be(1);
            var failure = responses.Single(response => !response.IsSuccessStatusCode);
            failure.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await failure.Content.ReadAsStringAsync()).Should().Contain("tax_profile_pending_exists");
            using var history = await SendAsync(_owner, HttpMethod.Get, Mine);
            var savedHistory = await JsonAsync(history);
            savedHistory.GetProperty("totalCount").GetInt32().Should().Be(1);
            var existingId = savedHistory.GetProperty("items")[0].GetProperty("id").GetGuid();
            await using var direct = new AppDbContext(_options);
            var duplicateId = Guid.NewGuid();
            Func<Task> bypassApplication = async () => await direct.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "DriverTaxProfiles" ("Id", "DriverId", "EffectiveFrom", "Status", "Declaration", "SubmittedAt", "SubmittedBy")
                SELECT {duplicateId}, "DriverId", "EffectiveFrom", "Status", "Declaration", "SubmittedAt", "SubmittedBy"
                FROM "DriverTaxProfiles" WHERE "Id" = {existingId}
                """);
            var databaseGuard = await bypassApplication.Should().ThrowAsync<PostgresException>();
            databaseGuard.Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
            databaseGuard.Which.ConstraintName.Should().Be("UX_DriverTaxProfiles_Pending");
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task ApprovalRequiresEmployerConfigurationAndCurrentDate_SelectionIgnoresFutureVersions()
    {
        var profile = await SubmitAsync(_owner, new(2026, 9, 12));
        using var missing = await SendAsync(_admin, HttpMethod.Post, $"{Profiles}/{profile}/approve", new
        { effectiveFrom = "2026-09-12", confirmed = true, kiwiSaverEmployer = (object?)null, holidayPay = (object?)null });
        missing.IsSuccessStatusCode.Should().BeFalse();
        using var oldDate = await SendAsync(_admin, HttpMethod.Post, $"{Profiles}/{profile}/approve", Approval(new(2026, 9, 11)));
        await AssertProblemAsync(oldDate, "tax_profile_effective_date_invalid");
        await ApproveAsync(profile, new(2026, 9, 12));
        var next = await SubmitAsync(_owner, new(2026, 9, 20));
        await ApproveAsync(next, new(2026, 9, 20));
        var slip = await CreatePayslipAsync(_owner);
        (await SelectAsync(slip, new(2026, 9, 14))).GetProperty("id").GetGuid().Should().Be(profile);
        (await SelectAsync(slip, new(2026, 9, 20))).GetProperty("id").GetGuid().Should().Be(next);
        var otherFuture = await SubmitAsync(_other, new(2026, 9, 20));
        using var advanced = await SendAsync(_admin, HttpMethod.Post, $"{Profiles}/{otherFuture}/approve", Approval(new(2026, 9, 12)));
        advanced.IsSuccessStatusCode.Should().BeFalse(await advanced.Content.ReadAsStringAsync());
        await AssertProblemAsync(advanced, "tax_profile_effective_date_invalid");
        await ApproveAsync(otherFuture, new(2026, 9, 20));
        var otherSlip = await CreatePayslipAsync(_other);
        (await SelectAsync(otherSlip, new(2026, 9, 14))).ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task SettlementRevalidatesDriverVersionAndPayDate_AndPreservesProvenanceWhenFinalised()
    {
        var first = await SubmitAsync(_owner, new(2026, 9, 12));
        await ApproveAsync(first, new(2026, 9, 12));
        var future = await SubmitAsync(_owner, new(2026, 9, 20));
        await ApproveAsync(future, new(2026, 9, 20));
        var otherProfile = await SubmitAsync(_other, new(2026, 9, 12));
        await ApproveAsync(otherProfile, new(2026, 9, 12));
        var slip = await CreatePayslipAsync(_owner);
        var route = $"/api/payroll/payslips/{slip}/settlement-from-profile";
        using var wrongDriver = await SendAsync(_admin, HttpMethod.Post, route, FromProfile(otherProfile, new(2026, 9, 14)));
        wrongDriver.IsSuccessStatusCode.Should().BeFalse();
        using var changedDate = await SendAsync(_admin, HttpMethod.Post, route, FromProfile(first, new(2026, 9, 21)));
        await AssertProblemAsync(changedDate, "tax_profile_stale_selection");
        using var forbidden = await SendAsync(_owner, HttpMethod.Post, route, FromProfile(first, new(2026, 9, 14)));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // A newly approved version invalidates an earlier selection even when the pay date is unchanged.
        var replacement = await SubmitAsync(_owner, new(2026, 9, 13));
        await ApproveAsync(replacement, new(2026, 9, 13));
        using var stale = await SendAsync(_admin, HttpMethod.Post, route, FromProfile(first, new(2026, 9, 14)));
        await AssertProblemAsync(stale, "tax_profile_stale_selection");
        using var accepted = await SendAsync(_admin, HttpMethod.Post, route, FromProfile(replacement, new(2026, 9, 14)));
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        var calculation = (await JsonAsync(accepted)).GetProperty("settlement");
        calculation.GetProperty("taxProfileId").GetGuid().Should().Be(replacement);
        calculation.GetProperty("request").GetProperty("grossEarnings").GetDecimal().Should().Be(1000m);
        calculation.GetProperty("calculation").GetProperty("baseGross").GetDecimal().Should().Be(1000m);
        Guid periodId;
        await using (var read = new AppDbContext(_options))
        {
            var saved = await read.Payslips.SingleAsync(item => item.Id == slip);
            periodId = saved.PayPeriodId;
            saved.Settlement.Should().NotBeNull();
            saved.Settlement!.Request.Employee!.TaxCode.Should().Be("M");
            saved.Settlement.Calculation.EmployeeKiwiSaver.Should().Be(35m);
        }
        using var finalise = await SendAsync(_admin, HttpMethod.Post, $"/api/payroll/periods/{periodId}/finalise", new { });
        finalise.StatusCode.Should().Be(HttpStatusCode.OK);
        using var immutable = await SendAsync(_admin, HttpMethod.Post, route, FromProfile(future, new(2026, 9, 21)));
        immutable.IsSuccessStatusCode.Should().BeFalse();
        await using var verify = new AppDbContext(_options);
        var finalised = await verify.Payslips.SingleAsync(item => item.Id == slip);
        var auditRows = await verify.AuditEvents.ToListAsync();
        auditRows.Count(row => row.Action == "SubmitTaxProfile").Should().Be(4);
        auditRows.Count(row => row.Action == "ApproveTaxProfile").Should().Be(4);
        var auditJson = JsonSerializer.Serialize(auditRows);
        foreach (var privateField in new[] { "taxCode", "kiwiSaverEmployee", "kiwiSaverEmployer", "withholdingRate", "approvedEmployee" })
            auditJson.Should().NotContainEquivalentOf(privateField);
        finalised.FinalisedAt.Should().NotBeNull();
        JsonSerializer.SerializeToElement(finalised.Settlement).GetProperty("TaxProfileId").GetGuid().Should().Be(replacement);
    }

    [Fact]
    public async Task RejectAndWithdrawKeepHistory_AndCannotBeApprovedAfterwards()
    {
        var rejected = await SubmitAsync(_owner, new(2026, 9, 12));
        using var reject = await SendAsync(_admin, HttpMethod.Post, $"{Profiles}/{rejected}/reject", new { });
        reject.IsSuccessStatusCode.Should().BeTrue();
        var withdrawn = await SubmitAsync(_owner, new(2026, 9, 13));
        using var withdraw = await SendAsync(_owner, HttpMethod.Post, $"{Profiles}/{withdrawn}/withdraw", new { });
        withdraw.IsSuccessStatusCode.Should().BeTrue();
        using var invalidApproval = await SendAsync(_admin, HttpMethod.Post, $"{Profiles}/{withdrawn}/approve", Approval(new(2026, 9, 13)));
        await AssertProblemAsync(invalidApproval, "tax_profile_not_pending");
        using var history = await SendAsync(_owner, HttpMethod.Get, Mine);
        var json = await JsonAsync(history);
        json.GetProperty("totalCount").GetInt32().Should().Be(2);
        json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("status").GetString())
            .Should().BeEquivalentTo(["Rejected", "Withdrawn"]);
    }

    [Fact]
    public async Task ContractorProfile_PreservesVerifiedInvoiceTreatmentWithoutEmployeeDeductions()
    {
        using var submit = await SendAsync(_owner, HttpMethod.Post, Mine, new
        {
            effectiveFrom = "2026-09-12", confirmed = true,
            declaration = new
            {
                workerType = "Contractor", employee = (object?)null,
                contractor = new { withholdingRate = 0.20m, withholdingVerified = true, exemptionVerified = false, gstRate = 0.15m, gstTreatmentVerified = true }
            }
        });
        submit.IsSuccessStatusCode.Should().BeTrue(await submit.Content.ReadAsStringAsync());
        var id = (await JsonAsync(submit)).GetProperty("id").GetGuid();
        using var approve = await SendAsync(_admin, HttpMethod.Post, $"{Profiles}/{id}/approve", new
        { effectiveFrom = "2026-09-12", confirmed = true, kiwiSaverEmployer = (object?)null, holidayPay = (object?)null });
        approve.IsSuccessStatusCode.Should().BeTrue(await approve.Content.ReadAsStringAsync());
        var approved = await JsonAsync(approve);
        approved.GetProperty("approvedEmployee").ValueKind.Should().Be(JsonValueKind.Null);
        approved.GetProperty("approvedContractor").GetProperty("withholdingRate").GetDecimal().Should().Be(0.20m);
        var slip = await CreatePayslipAsync(_owner);
        using var settled = await SendAsync(_admin, HttpMethod.Post, $"/api/payroll/payslips/{slip}/settlement-from-profile", FromProfile(id, new(2026, 9, 14)));
        settled.IsSuccessStatusCode.Should().BeTrue(await settled.Content.ReadAsStringAsync());
        var snapshot = (await JsonAsync(settled)).GetProperty("settlement");
        snapshot.GetProperty("taxProfileId").GetGuid().Should().Be(id);
        var calculation = snapshot.GetProperty("calculation");
        calculation.GetProperty("contractorWithholding").GetDecimal().Should().Be(200m);
        calculation.GetProperty("gst").GetDecimal().Should().Be(150m);
        calculation.GetProperty("netPay").GetDecimal().Should().Be(950m);
        foreach (var field in new[] { "paye", "accEarnersLevy", "employeeKiwiSaver", "employerKiwiSaverGross", "holidayPay" })
            calculation.GetProperty(field).GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task ExplicitDemoReseed_CleansExistingTaxProfilesBeforeReferencedDrivers()
    {
        await using var context = new AppDbContext(_options);
        // Audit-free development fixture: cleaning audited users remains blocked by the existing append-only audit policy.
        context.DriverTaxProfiles.Add(new DriverTaxProfile(Guid.NewGuid(), _owner.DriverId!.Value, new(2026, 9, 12),
            new DriverTaxDeclaration(SettlementWorkerType.Employee, new EmployeeTaxDeclaration("M", new(0.035m, true, false)), null),
            _clock.UtcNow, _owner.UserId));
        await context.SaveChangesAsync();
        (await context.DriverTaxProfiles.CountAsync()).Should().Be(1);
        (await context.AuditEvents.CountAsync()).Should().Be(0);
        await Nimpression.Infrastructure.Persistence.Seed.DatabaseSeeder.SeedAsync(context, cleanExisting: true);
        context.ChangeTracker.Clear();
        (await context.DriverTaxProfiles.CountAsync()).Should().Be(0);
        (await context.Drivers.AnyAsync(driver => driver.Id == _owner.DriverId)).Should().BeFalse();
        (await context.Drivers.CountAsync()).Should().Be(10);
    }

    private static object Submission(DateOnly effectiveFrom, bool confirmed = true) => new
    {
        effectiveFrom, confirmed, declaration = new
        {
            workerType = "Employee", employee = new
            {
                taxCode = "M", kiwiSaverEmployee = new { rate = 0.035m, contributionsRequired = true, temporaryReductionApproved = false },
                // This is deliberately untrusted: driver declarations cannot set employer policy.
                kiwiSaverEmployer = new { rate = 0.99m, contributionsRequired = false, esctRate = 0.39m, esctRateVerified = true }
            }, contractor = (object?)null
        }
    };
    private static object Approval(DateOnly effectiveFrom) => new
    {
        effectiveFrom, confirmed = true,
        kiwiSaverEmployer = new { rate = 0.035m, contributionsRequired = true, temporaryReductionApproved = false, esctRate = 0.175m, esctRateVerified = true },
        holidayPay = new { mode = "OrdinaryAccrual", eligibilityConfirmed = false, writtenAgreementConfirmed = false, eligibility = (object?)null }
    };
    private static object FromProfile(Guid profileId, DateOnly payDate) => new { profileId, payDate, frequency = "Fortnightly" };

    private async Task<Guid> SubmitAsync(Actor actor, DateOnly effectiveFrom)
    {
        using var response = await SendAsync(actor, HttpMethod.Post, Mine, Submission(effectiveFrom));
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var submitted = await JsonAsync(response);
        submitted.GetProperty("effectiveFrom").GetString().Should().Be(effectiveFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        return submitted.GetProperty("id").GetGuid();
    }
    private async Task ApproveAsync(Guid id, DateOnly effectiveFrom)
    {
        using var response = await SendAsync(_admin, HttpMethod.Post, $"{Profiles}/{id}/approve", Approval(effectiveFrom));
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }
    private async Task<JsonElement> SelectAsync(Guid slip, DateOnly payDate)
    {
        using var response = await SendAsync(_admin, HttpMethod.Get, $"/api/payroll/payslips/{slip}/tax-profile?payDate={payDate:yyyy-MM-dd}");
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        if (response.StatusCode == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()))
            return JsonSerializer.SerializeToElement<object?>(null);
        return await JsonAsync(response);
    }
    private async Task<Guid> CreatePayslipAsync(Actor actor)
    {
        await using var context = new AppDbContext(_options);
        var period = await context.PayPeriods.SingleOrDefaultAsync(item => item.StartsOn == new DateOnly(2026, 9, 1));
        if (period is null)
        {
            period = new PayPeriod(Guid.NewGuid(), new(2026, 9, 1), new(2026, 9, 14));
            context.PayPeriods.Add(period);
        }
        var slip = new Payslip(Guid.NewGuid(), period.Id, actor.DriverId!.Value, new WorkHours(10), WorkHours.Zero, WorkHours.Zero,
            new Money(100), new Money(1000), 0, Kilometres.Zero, Money.Zero(), Money.Zero(), Money.Zero(), PayBasis.Hourly, new Money(1000), false, _clock.UtcNow);
        context.Payslips.Add(slip); await context.SaveChangesAsync(); return slip.Id;
    }
    private async Task<HttpResponseMessage> SendAsync(Actor actor, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens[actor.UserId]);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
    private static async Task AssertProblemAsync(HttpResponseMessage response, string code)
    {
        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().Contain(code);
    }
    private sealed record Actor(Guid UserId, Guid? DriverId, string Email, UserRole Role)
    {
        public static Actor Create(UserRole role) => new(Guid.NewGuid(), role == UserRole.Driver ? Guid.NewGuid() : null, TestDataFactory.CreateEmail("tax_profile"), role);
    }
}
