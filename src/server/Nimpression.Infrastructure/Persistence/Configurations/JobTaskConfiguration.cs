using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class JobTaskConfiguration : IEntityTypeConfiguration<JobTask>
{
    public void Configure(EntityTypeBuilder<JobTask> builder)
    {
        builder.ToTable("JobTasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Ref)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(t => t.Ref)
            .IsUnique();

        builder.Property(t => t.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnType("text");

        builder.Property(t => t.AreaId)
            .IsRequired();

        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(t => t.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.VehicleId);

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(t => t.VehicleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(t => t.DriverId);

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(t => t.ScheduledFor)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(t => t.Priority)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.AcknowledgedAt)
            .HasColumnType("timestamptz");

        builder.Property(t => t.StartedAt)
            .HasColumnType("timestamptz");

        builder.Property(t => t.CompletedAt)
            .HasColumnType("timestamptz");

        builder.Property(t => t.CancelledAt)
            .HasColumnType("timestamptz");

        builder.Property(t => t.CancellationReason)
            .HasColumnType("text");

        builder.Property(t => t.CreatedByUserId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.PlannedDistanceKm)
            .HasConversion(
                km => km.HasValue ? km.Value.Value : (decimal?)null,
                val => val.HasValue ? new Kilometres(val.Value) : (Kilometres?)null)
            .HasColumnType("numeric(18,2)");

        builder.Property(t => t.ActualDistanceKm)
            .HasConversion(
                km => km.HasValue ? km.Value.Value : (decimal?)null,
                val => val.HasValue ? new Kilometres(val.Value) : (Kilometres?)null)
            .HasColumnType("numeric(18,2)");

        builder.Property(t => t.StartOdometerKm)
            .HasConversion(
                km => km.HasValue ? km.Value.Value : (decimal?)null,
                val => val.HasValue ? new Kilometres(val.Value) : (Kilometres?)null)
            .HasColumnType("numeric(18,2)");

        builder.Property(t => t.EndOdometerKm)
            .HasConversion(
                km => km.HasValue ? km.Value.Value : (decimal?)null,
                val => val.HasValue ? new Kilometres(val.Value) : (Kilometres?)null)
            .HasColumnType("numeric(18,2)");

        builder.Ignore(t => t.EffectiveDistanceKm);

        builder.HasIndex(t => new { t.DriverId, t.Status, t.ScheduledFor });
        builder.HasIndex(t => new { t.AreaId, t.ScheduledFor });
    }
}
