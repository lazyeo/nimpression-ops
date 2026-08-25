using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence.Seed;
using Nimpression.Infrastructure.Storage;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Drivers;

[Collection("PostgreSqlCollection")]
public sealed class DriverRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public DriverRepositoryTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(context, SeedConstants.DefaultSeed, cleanExisting: false);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetDriversPagedAsync_with_default_filter_returns_all_active_drivers_projected()
    {
        await using var context = _fixture.CreateDbContext();
        var sut = new DriverRepository(context);

        var filter = new DriverFilter(Page: 1, PageSize: 20);
        var today = SeedConstants.ReferenceDate;

        var result = await sut.GetDriversPagedAsync(filter, today);

        result.TotalCount.Should().BeGreaterThanOrEqualTo(10);
        result.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(d =>
        {
            d.EmployeeNo.Should().StartWith("DRV-");
            d.DisplayName.Should().NotBeNullOrWhiteSpace();
            d.Email.Should().Contain("@nimpression.co.nz");
            d.HourlyRate.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task GetDriversPagedAsync_filter_by_name_returns_matching_driver()
    {
        await using var context = _fixture.CreateDbContext();
        var sut = new DriverRepository(context);

        var filter = new DriverFilter(Name: "Liam", Page: 1, PageSize: 20);
        var today = SeedConstants.ReferenceDate;

        var result = await sut.GetDriversPagedAsync(filter, today);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(d => d.DisplayName == "Liam Smith");
    }

    [Fact]
    public async Task GetExpiringLicencesAsync_detects_driver_expiring_within_30_days()
    {
        await using var context = _fixture.CreateDbContext();
        var sut = new DriverRepository(context);
        var today = SeedConstants.ReferenceDate;

        var alerts = await sut.GetExpiringLicencesAsync(today, daysThreshold: 30);

        alerts.Should().NotBeEmpty();
        alerts.Should().Contain(a => a.EmployeeNo == "DRV-004");
        var drv4 = alerts.First(a => a.EmployeeNo == "DRV-004");
        drv4.DisplayName.Should().Be("Jack Brown");
        drv4.DaysUntilExpiry.Should().BeInRange(0, 30);
    }

    [Fact]
    public async Task GetDriverDetailByIdAsync_returns_full_detail_with_areas()
    {
        await using var context = _fixture.CreateDbContext();
        var sut = new DriverRepository(context);
        var today = SeedConstants.ReferenceDate;

        var driver = await context.Drivers.FirstAsync(d => d.EmployeeNo == "DRV-001");
        var detail = await sut.GetDriverDetailByIdAsync(driver.Id, today);

        detail.Should().NotBeNull();
        detail!.EmployeeNo.Should().Be("DRV-001");
        detail.DisplayName.Should().Be("Liam Smith");
        detail.HourlyRateAmount.Should().Be(32.50m);
        detail.AreaAssignments.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddDriverAsync_persists_driver_and_user_and_assignments()
    {
        await using var context = _fixture.CreateDbContext();
        var sut = new DriverRepository(context);

        var userId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var today = SeedConstants.ReferenceDate;
        var employeeNo = TestDataFactory.CreateEmployeeNo("DRV");

        var user = new User(
            userId,
            TestDataFactory.CreateEmailAddress("test.integration"),
            "hash",
            UserRole.Driver,
            "Integration Test Driver",
            "en-NZ");

        var driver = new Driver(
            driverId,
            userId,
            employeeNo,
            "Class 5",
            today.AddYears(2),
            new Money(40.00m),
            new Money(55.00m),
            new Money(1.10m),
            "ENC(021123456)",
            "ENC(123 Test St)",
            "ENC(021999888)",
            today,
            DriverStatus.Active);

        await sut.AddDriverAsync(driver, user);
        await context.SaveChangesAsync();

        var persistedDriver = await sut.GetByIdAsync(driverId);
        persistedDriver.Should().NotBeNull();
        persistedDriver!.EmployeeNo.Should().Be(employeeNo);
        persistedDriver.HourlyRate.Amount.Should().Be(40.00m);

        var persistedUser = await sut.GetUserByIdAsync(userId);
        persistedUser.Should().NotBeNull();
        persistedUser!.DisplayName.Should().Be("Integration Test Driver");
    }
}
