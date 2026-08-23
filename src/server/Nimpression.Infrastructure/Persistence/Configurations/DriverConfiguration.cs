using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.UserId)
            .IsRequired();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<Driver>(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.UserId)
            .IsUnique();

        builder.Property(d => d.EmployeeNo)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(d => d.EmployeeNo)
            .IsUnique();

        builder.Property(d => d.LicenceClass)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.LicenceExpiry)
            .HasColumnType("date")
            .IsRequired();

        builder.ComplexProperty(d => d.HourlyRate, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("HourlyRateAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("HourlyRateCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(d => d.PerTripRate, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("PerTripRateAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("PerTripRateCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(d => d.PerKmRate, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("PerKmRateAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("PerKmRateCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(d => d.PhoneEnc)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.AddressEnc)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(d => d.EmergencyContactEnc)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(d => d.HiredOn)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
    }
}
