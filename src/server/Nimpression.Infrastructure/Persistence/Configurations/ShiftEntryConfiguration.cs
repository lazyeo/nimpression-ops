using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Entities.Vehicle;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class ShiftEntryConfiguration : IEntityTypeConfiguration<ShiftEntry>
{
    public void Configure(EntityTypeBuilder<ShiftEntry> builder)
    {
        builder.ToTable("ShiftEntries");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.DriverId)
            .IsRequired();

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(s => s.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.ClockInAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(s => s.ClockInLat)
            .HasColumnType("numeric(10,7)");

        builder.Property(s => s.ClockInLng)
            .HasColumnType("numeric(10,7)");

        builder.Property(s => s.ClockOutAt)
            .HasColumnType("timestamptz");

        builder.Property(s => s.ClockOutLat)
            .HasColumnType("numeric(10,7)");

        builder.Property(s => s.ClockOutLng)
            .HasColumnType("numeric(10,7)");

        builder.Property(s => s.VehicleId);

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(s => s.VehicleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(s => s.BreakMinutes)
            .IsRequired();

        builder.Property(s => s.Note)
            .HasColumnType("text");

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.AdminCorrectionReason)
            .HasColumnType("text");

        builder.Property(s => s.CorrectedByUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.CorrectedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(s => s.CorrectedAt)
            .HasColumnType("timestamptz");

        builder.HasIndex(s => new { s.DriverId, s.ClockInAt });
        builder.HasIndex(s => new { s.DriverId, s.Status });
    }
}
