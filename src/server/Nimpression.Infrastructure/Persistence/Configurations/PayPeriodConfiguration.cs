using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Payroll;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class PayPeriodConfiguration : IEntityTypeConfiguration<PayPeriod>
{
    public void Configure(EntityTypeBuilder<PayPeriod> builder)
    {
        builder.ToTable("PayPeriods");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.StartsOn)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(p => p.EndsOn)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.FinalisedAt)
            .HasColumnType("timestamptz");

        builder.Property(p => p.PaidAt)
            .HasColumnType("timestamptz");

        builder.HasIndex(p => new { p.StartsOn, p.EndsOn })
            .IsUnique();
    }
}
