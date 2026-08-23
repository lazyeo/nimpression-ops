using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Vehicle;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class IncidentReportConfiguration : IEntityTypeConfiguration<IncidentReport>
{
    public void Configure(EntityTypeBuilder<IncidentReport> builder)
    {
        builder.ToTable("IncidentReports");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.DriverId)
            .IsRequired();

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(i => i.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.VehicleId)
            .IsRequired();

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(i => i.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.OccurredAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(i => i.Location)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Severity)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(i => i.ThirdPartyInfoEnc)
            .HasColumnType("text");

        builder.Property(i => i.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.InsurerNotifiedAt)
            .HasColumnType("timestamptz");

        builder.Property(i => i.PhotoKeys)
            .HasField("_photoKeys")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v)
                    ? new List<string>()
                    : (JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()))
            .HasColumnType("text");

        builder.Ignore(i => i.ShouldNotifyInsurer);

        builder.HasIndex(i => new { i.DriverId, i.OccurredAt });
        builder.HasIndex(i => new { i.VehicleId, i.OccurredAt });
    }
}
