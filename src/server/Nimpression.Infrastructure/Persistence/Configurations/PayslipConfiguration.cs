using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> builder)
    {
        builder.ToTable("Payslips");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PayPeriodId)
            .IsRequired();

        builder.HasOne<PayPeriod>()
            .WithMany()
            .HasForeignKey(p => p.PayPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.DriverId)
            .IsRequired();

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(p => p.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.OrdinaryHours)
            .HasConversion(
                w => w.Value,
                v => new WorkHours(v))
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(p => p.OvertimeHours)
            .HasConversion(
                w => w.Value,
                v => new WorkHours(v))
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(p => p.HolidayHours)
            .HasConversion(
                w => w.Value,
                v => new WorkHours(v))
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.ComplexProperty(p => p.HourlyRateSnapshot, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("HourlyRateSnapshotAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("HourlyRateSnapshotCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(p => p.HoursBasedGross, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("HoursBasedGrossAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("HoursBasedGrossCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(p => p.CompletedTripCount)
            .IsRequired();

        builder.Property(p => p.TotalDistanceKm)
            .HasConversion(
                k => k.Value,
                v => new Kilometres(v))
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.ComplexProperty(p => p.PerTripRateSnapshot, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("PerTripRateSnapshotAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("PerTripRateSnapshotCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(p => p.PerKmRateSnapshot, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("PerKmRateSnapshotAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("PerKmRateSnapshotCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(p => p.TripBasedGross, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("TripBasedGrossAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("TripBasedGrossCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(p => p.BasisUsed)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.ComplexProperty(p => p.GrossPay, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("GrossPayAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("GrossPayCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(p => p.MinimumWageTopUp)
            .IsRequired();

        builder.Property(p => p.CalculatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(p => p.FinalisedAt)
            .HasColumnType("timestamptz");

        builder.HasMany(p => p.Lines)
            .WithOne()
            .HasForeignKey(l => l.PayslipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => new { p.PayPeriodId, p.DriverId })
            .IsUnique();
    }
}
