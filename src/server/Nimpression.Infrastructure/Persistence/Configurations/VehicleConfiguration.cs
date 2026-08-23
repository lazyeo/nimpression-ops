using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Rego)
            .HasConversion(
                rego => rego.Value,
                value => new Rego(value))
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(v => v.Rego)
            .IsUnique();

        builder.Property(v => v.Make)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.Model)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.Year)
            .IsRequired();

        builder.Property(v => v.VinEnc)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(v => v.OdometerKm)
            .HasConversion(
                km => km.Value,
                value => new Kilometres(value))
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(v => v.ServiceIntervalKm)
            .HasConversion(
                km => km.Value,
                value => new Kilometres(value))
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(v => v.LastServiceOdometerKm)
            .HasConversion(
                km => km.Value,
                value => new Kilometres(value))
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(v => v.WofExpiry)
            .HasColumnType("date");

        builder.Property(v => v.CofExpiry)
            .HasColumnType("date");

        builder.Property(v => v.InsuranceExpiry)
            .HasColumnType("date");

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
    }
}
