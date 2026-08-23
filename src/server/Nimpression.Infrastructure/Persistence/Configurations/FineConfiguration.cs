using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class FineConfiguration : IEntityTypeConfiguration<Fine>
{
    public void Configure(EntityTypeBuilder<Fine> builder)
    {
        builder.ToTable("Fines");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.DriverId)
            .IsRequired();

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(f => f.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.VehicleId)
            .IsRequired();

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(f => f.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.IssuedOn)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(f => f.Authority)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.Reference)
            .HasMaxLength(100)
            .IsRequired();

        builder.ComplexProperty(f => f.Amount, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("Amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(f => f.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.TicketPhotoKey)
            .HasMaxLength(200);

        builder.Property(f => f.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(f => f.ReviewedByUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(f => f.ReviewedAt)
            .HasColumnType("timestamptz");

        builder.Property(f => f.ReviewNote)
            .HasColumnType("text");

        builder.HasIndex(f => new { f.DriverId, f.Status });
        builder.HasIndex(f => new { f.VehicleId, f.IssuedOn });
    }
}
