using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class PayslipLineConfiguration : IEntityTypeConfiguration<PayslipLine>
{
    public void Configure(EntityTypeBuilder<PayslipLine> builder)
    {
        builder.ToTable("PayslipLines");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.PayslipId)
            .IsRequired();

        builder.HasOne<Payslip>()
            .WithMany(p => p.Lines)
            .HasForeignKey(l => l.PayslipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(l => l.Basis)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.Kind)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.Description)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(l => l.Hours)
            .HasConversion(
                w => w.HasValue ? w.Value.Value : (decimal?)null,
                val => val.HasValue ? new WorkHours(val.Value) : (WorkHours?)null)
            .HasColumnType("numeric(18,2)");

        builder.Property(l => l.Distance)
            .HasConversion(
                k => k.HasValue ? k.Value.Value : (decimal?)null,
                val => val.HasValue ? new Kilometres(val.Value) : (Kilometres?)null)
            .HasColumnType("numeric(18,2)");

        builder.Property(l => l.Qty);

        builder.ComplexProperty(l => l.Rate, b =>
        {
            b.Property(m => m.Amount)
                .HasColumnName("RateAmount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            b.Property(m => m.Currency)
                .HasColumnName("RateCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(l => l.Amount, b =>
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

        builder.HasIndex(l => l.PayslipId);
    }
}
