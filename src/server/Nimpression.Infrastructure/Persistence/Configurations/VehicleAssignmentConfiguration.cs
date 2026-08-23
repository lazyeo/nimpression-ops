using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class VehicleAssignmentConfiguration : IEntityTypeConfiguration<VehicleAssignment>
{
    public void Configure(EntityTypeBuilder<VehicleAssignment> builder)
    {
        builder.ToTable("VehicleAssignments");

        builder.HasKey(va => va.Id);

        builder.Property(va => va.VehicleId)
            .IsRequired();

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(va => va.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(va => va.DriverId)
            .IsRequired();

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(va => va.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(va => va.AssignedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(va => va.ReleasedAt)
            .HasColumnType("timestamptz");

        builder.Property(va => va.AssignedByUserId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(va => va.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // 部分唯一索引：保证一台车在同一时刻至多只能有一个未释放的分派 (WHERE "ReleasedAt" IS NULL)
        builder.HasIndex(va => va.VehicleId)
            .IsUnique()
            .HasFilter("\"ReleasedAt\" IS NULL");

        builder.HasIndex(va => va.DriverId);
    }
}
