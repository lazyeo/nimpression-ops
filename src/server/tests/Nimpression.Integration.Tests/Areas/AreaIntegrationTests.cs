using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Commands.AssignDriverToArea;
using Nimpression.Application.Features.Areas.Commands.CreateArea;
using Nimpression.Application.Features.Areas.Commands.DeleteArea;
using Nimpression.Application.Features.Areas.Commands.EndAreaAssignment;
using Nimpression.Application.Features.Areas.Commands.UpdateArea;
using Nimpression.Application.Features.Areas.DTOs;
using Nimpression.Application.Features.Areas.Queries.GetAreaAssignments;
using Nimpression.Application.Features.Areas.Queries.GetAreaById;
using Nimpression.Application.Features.Areas.Queries.GetAreasList;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Repositories;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Areas;

[Collection("PostgreSqlCollection")]
public class AreaIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public AreaIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string GenerateAreaCode(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..6].ToUpperInvariant()}";
    }

    private async Task<Driver> SeedDriverAsync()
    {
        await using var context = _fixture.CreateDbContext();

        var user = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("driver"),
            "hash",
            UserRole.Driver,
            "Integration Driver",
            "en-NZ",
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));

        var driver = new Driver(
            Guid.NewGuid(),
            user.Id,
            TestDataFactory.CreateEmployeeNo("DRV"),
            "Class 4",
            new DateOnly(2028, 1, 1),
            new Money(32m),
            new Money(45m),
            new Money(0.85m),
            "ENC(021123456)",
            "ENC(123 Main Rd)",
            "ENC(Emergency Contact)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        await context.Users.AddAsync(user);
        await context.Drivers.AddAsync(driver);
        await context.SaveChangesAsync();

        return driver;
    }

    #region F4.1 区域 CRUD 与唯一约束

    [Fact]
    public async Task F4_1_CreateArea_Success_PersistsToDatabase()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new AreaRepository(context);
        var uow = new UnitOfWork(context);
        var handler = new CreateAreaCommandHandler(repo, uow);

        var uniqueCode = GenerateAreaCode("AKL");
        var command = new CreateAreaCommand(
            "Test Area",
            uniqueCode,
            "Description for test area",
            "{\"type\":\"Polygon\"}",
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        await using var verifyContext = _fixture.CreateDbContext();
        var saved = await verifyContext.Areas.FindAsync(result.Value);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test Area");
        saved.Code.Should().Be(uniqueCode);
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task F4_1_CreateArea_DuplicateCode_Returns409Conflict()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var repo = new AreaRepository(context);
        var uow = new UnitOfWork(context);
        var handler = new CreateAreaCommandHandler(repo, uow);

        var uniqueCode = GenerateAreaCode("DUP");

        var command1 = new CreateAreaCommand("Area One", uniqueCode);
        var command2 = new CreateAreaCommand("Area Two", uniqueCode);

        // Act: 第一次创建成功
        var result1 = await handler.Handle(command1, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        // Act: 第二次创建相同代码触发 409 Conflict
        var result2 = await handler.Handle(command2, CancellationToken.None);

        // Assert
        result2.IsSuccess.Should().BeFalse();
        result2.Error.Should().NotBeNull();
        result2.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result2.Error.Code.Should().Be("area_code_conflict");
    }

    [Fact]
    public async Task F4_1_DeleteArea_WithActiveAssignments_Returns409Conflict()
    {
        // Arrange
        var driver = await SeedDriverAsync();

        await using var context = _fixture.CreateDbContext();
        var repo = new AreaRepository(context);
        var uow = new UnitOfWork(context);
        var dtProvider = new TestDateTimeProvider();

        var area = new Area(
            Guid.NewGuid(),
            "Protected Area",
            GenerateAreaCode("PROT"));
        await context.Areas.AddAsync(area);

        var assignment = new AreaAssignment(
            Guid.NewGuid(),
            area.Id,
            driver.Id,
            dtProvider.NzToday.AddDays(-5),
            null); // 活跃中
        await context.AreaAssignments.AddAsync(assignment);
        await context.SaveChangesAsync();

        var deleteHandler = new DeleteAreaCommandHandler(repo, uow, dtProvider);

        // Act
        var result = await deleteHandler.Handle(new DeleteAreaCommand(area.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be("area_has_active_assignments");

        // 验证区域依然存在
        await using var verifyContext = _fixture.CreateDbContext();
        var exists = await verifyContext.Areas.AnyAsync(a => a.Id == area.Id);
        exists.Should().BeTrue();
    }

    #endregion

    #region F4.2 分配生效期与重叠约束

    [Fact]
    public async Task F4_2_AssignDriverToArea_OverlappingPeriod_Returns422Unprocessable()
    {
        // Arrange
        var driver = await SeedDriverAsync();

        await using var context = _fixture.CreateDbContext();
        var repo = new AreaRepository(context);
        var uow = new UnitOfWork(context);

        var area = new Area(
            Guid.NewGuid(),
            "Overlap Test Area",
            GenerateAreaCode("OVL"));
        await context.Areas.AddAsync(area);
        await context.SaveChangesAsync();

        var handler = new AssignDriverToAreaCommandHandler(repo, uow);

        // 第一次分配：2026-01-01 .. 2026-06-30
        var command1 = new AssignDriverToAreaCommand(
            driver.Id,
            area.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));
        var result1 = await handler.Handle(command1, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        // 第二次分配：2026-04-01 .. 2026-09-30 (与第一次重叠)
        var command2 = new AssignDriverToAreaCommand(
            driver.Id,
            area.Id,
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 9, 30));
        var result2 = await handler.Handle(command2, CancellationToken.None);

        // Assert: 422 且包含冲突区间
        result2.IsSuccess.Should().BeFalse();
        result2.Error.Should().NotBeNull();
        result2.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result2.Error.Code.Should().Be("area_assignment_overlap");
        result2.Error.Message.Should().Contain("2026-01-01..2026-06-30");
        result2.Error.Message.Should().Contain("2026-04-01..2026-09-30");
    }

    [Fact]
    public async Task F4_2_AssignDriverToArea_NonOverlapping_Succeeds()
    {
        // Arrange
        var driver = await SeedDriverAsync();

        await using var context = _fixture.CreateDbContext();
        var repo = new AreaRepository(context);
        var uow = new UnitOfWork(context);

        var area = new Area(
            Guid.NewGuid(),
            "Sequential Test Area",
            GenerateAreaCode("SEQ"));
        await context.Areas.AddAsync(area);
        await context.SaveChangesAsync();

        var handler = new AssignDriverToAreaCommandHandler(repo, uow);

        // 第一次分配：2026-01-01 .. 2026-06-30
        var command1 = new AssignDriverToAreaCommand(
            driver.Id,
            area.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));
        var result1 = await handler.Handle(command1, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        // 第二次分配：2026-07-01 .. 2026-12-31 (不重叠)
        var command2 = new AssignDriverToAreaCommand(
            driver.Id,
            area.Id,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 12, 31));
        var result2 = await handler.Handle(command2, CancellationToken.None);

        // Assert: 成功
        result2.IsSuccess.Should().BeTrue();

        await using var verifyContext = _fixture.CreateDbContext();
        var assignments = await verifyContext.AreaAssignments
            .Where(aa => aa.AreaId == area.Id && aa.DriverId == driver.Id)
            .ToListAsync();
        assignments.Should().HaveCount(2);
    }

    #endregion

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset NzNow => UtcNow.ToOffset(TimeSpan.FromHours(12));
        public DateOnly NzToday => DateOnly.FromDateTime(NzNow.DateTime);
    }
}
