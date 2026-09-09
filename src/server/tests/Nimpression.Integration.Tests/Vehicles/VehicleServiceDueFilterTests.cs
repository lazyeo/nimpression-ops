using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Vehicles.DTOs;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence.Repositories;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Vehicles;

[Collection("PostgreSqlCollection")]
public sealed class VehicleServiceDueFilterTests(PostgreSqlContainerFixture fixture)
{
    [Fact]
    public async Task ServiceDueOnly_FiltersBeforeCountingAndPaging_AtTheMileageThreshold()
    {
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
        var make = $"DueFilter-{Guid.NewGuid():N}";
        Vehicle Create(decimal odometer, VehicleStatus status = VehicleStatus.Active) => new(
            Guid.NewGuid(), TestDataFactory.CreateRegoObject(), make, "Test model", 2020,
            "test-vin", new Kilometres(odometer), new Kilometres(10000m),
            new Kilometres(5000m), status: status);

        // Independent expected boundaries: service recorded at 5,000 + interval 10,000.
        var below = Create(14999.99m);
        var at = Create(15000m);
        var above = Create(15000.01m);
        var inactiveDue = Create(16000m, VehicleStatus.Decommissioned);
        context.Vehicles.AddRange(below, at, above, inactiveDue);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new VehicleRepository(context);
        var dueFilter = new VehicleFilter(Search: make, Status: VehicleStatus.Active, ServiceDueOnly: true, PageSize: 1);

        var firstPage = await repository.GetVehiclesPagedAsync(dueFilter);
        var secondPage = await repository.GetVehiclesPagedAsync(dueFilter with { Page = 2 });
        firstPage.TotalCount.Should().Be(2);
        secondPage.TotalCount.Should().Be(2);
        firstPage.Items.Should().ContainSingle();
        secondPage.Items.Should().ContainSingle();
        firstPage.Items.Concat(secondPage.Items).Select(vehicle => vehicle.Id)
            .Should().BeEquivalentTo([at.Id, above.Id]);
        firstPage.Items.Concat(secondPage.Items).Should().OnlyContain(vehicle => vehicle.IsServiceDue);

        foreach (var disabled in new bool?[] { false, null })
        {
            var all = await repository.GetVehiclesPagedAsync(dueFilter with { ServiceDueOnly = disabled, PageSize = 20 });
            all.TotalCount.Should().Be(3);
            all.Items.Select(vehicle => vehicle.Id).Should().BeEquivalentTo([below.Id, at.Id, above.Id]);
        }
    }
}
