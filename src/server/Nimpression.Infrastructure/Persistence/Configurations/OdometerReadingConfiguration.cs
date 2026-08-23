using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class OdometerReadingConfiguration : IEntityTypeConfiguration<OdometerReading>
{
    public void Configure(EntityTypeBuilder<OdometerReading> builder)
    {
        builder.ToTable("OdometerReadings");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.VehicleId)
            .IsRequired();

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.DriverId)
            .IsRequired();

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(r => r.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.ReadingKm)
            .HasConversion(
                km => km.Value,
                value => new Kilometres(value))
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(r => r.PhotoKey)
            .HasMaxLength(200);

        builder.Property(r => r.RecordedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.Source)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(r => new { r.VehicleId, r.RecordedAt });
        builder.HasIndex(r => r.DriverId);
    }
}
