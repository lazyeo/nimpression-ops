using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Tests.Entities;

public sealed class AreaTests
{
    [Fact]
    public void Area_initializes_and_updates_details()
    {
        var id = Guid.NewGuid();
        var area = new Area(id, "Auckland Central", "akl-cen", "CBD area", "{\"type\":\"Polygon\"}");

        Assert.Equal(id, area.Id);
        Assert.Equal("Auckland Central", area.Name);
        Assert.Equal("AKL-CEN", area.Code);
        Assert.Equal("CBD area", area.Description);
        Assert.Equal("{\"type\":\"Polygon\"}", area.GeoJson);
        Assert.True(area.IsActive);

        area.UpdateDetails("North Shore", "akl-nth", null, null);
        Assert.Equal("North Shore", area.Name);
        Assert.Equal("AKL-NTH", area.Code);
        Assert.Null(area.Description);
        Assert.Null(area.GeoJson);

        area.Deactivate();
        Assert.False(area.IsActive);
        area.Activate();
        Assert.True(area.IsActive);
    }

    [Fact]
    public void Area_throws_on_empty_name_or_code()
    {
        Assert.Throws<DomainValidationException>(() => new Area(Guid.NewGuid(), "", "CODE"));
        Assert.Throws<DomainValidationException>(() => new Area(Guid.NewGuid(), "Name", "  "));
    }

    [Fact]
    public void AreaAssignment_effective_ranges_and_overlap_matrix()
    {
        var areaId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 6, 30);

        var assignment = new AreaAssignment(Guid.NewGuid(), areaId, driverId, from, to);

        Assert.True(assignment.IsEffectiveOn(new DateOnly(2026, 1, 1)));
        Assert.True(assignment.IsEffectiveOn(new DateOnly(2026, 3, 15)));
        Assert.True(assignment.IsEffectiveOn(new DateOnly(2026, 6, 30)));
        Assert.False(assignment.IsEffectiveOn(new DateOnly(2025, 12, 31)));
        Assert.False(assignment.IsEffectiveOn(new DateOnly(2026, 7, 1)));

        // Overlap checks
        Assert.True(assignment.OverlapsWith(new DateOnly(2026, 5, 1), new DateOnly(2026, 7, 1)));
        Assert.True(assignment.OverlapsWith(new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 1)));
        Assert.False(assignment.OverlapsWith(new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1)));
        Assert.False(assignment.OverlapsWith(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));

        // Open-ended overlap
        var openEnded = new AreaAssignment(Guid.NewGuid(), areaId, driverId, new DateOnly(2026, 7, 1));
        Assert.True(openEnded.IsEffectiveOn(new DateOnly(2028, 1, 1)));
        Assert.True(openEnded.OverlapsWith(new DateOnly(2026, 8, 1), null));

        openEnded.EndAssignment(new DateOnly(2026, 12, 31));
        Assert.Equal(new DateOnly(2026, 12, 31), openEnded.EffectiveTo);
        Assert.False(openEnded.IsEffectiveOn(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void AreaAssignment_throws_when_end_is_before_start()
    {
        Assert.Throws<DomainValidationException>(() => new AreaAssignment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 5, 31)));
    }
}
