using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Driver;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class AreaAssignmentConfiguration : IEntityTypeConfiguration<AreaAssignment>
{
    public void Configure(EntityTypeBuilder<AreaAssignment> builder)
    {
        builder.ToTable("AreaAssignments");

        builder.HasKey(aa => aa.Id);

        builder.Property(aa => aa.AreaId)
            .IsRequired();

        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(aa => aa.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(aa => aa.DriverId)
            .IsRequired();

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(aa => aa.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(aa => aa.EffectiveFrom)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(aa => aa.EffectiveTo)
            .HasColumnType("date");

        builder.HasIndex(aa => new { aa.DriverId, aa.AreaId });
        builder.HasIndex(aa => new { aa.AreaId, aa.EffectiveFrom });
    }
}
