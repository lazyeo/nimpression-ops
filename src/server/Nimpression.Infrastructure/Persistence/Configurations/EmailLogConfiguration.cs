using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("EmailLogs");

        builder.HasKey(el => el.Id);

        builder.Property(el => el.TemplateKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(el => el.ToAddress)
            .HasConversion(
                email => email.Value,
                value => new EmailAddress(value))
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(el => el.Subject)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(el => el.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(el => el.Attempts)
            .IsRequired();

        builder.Property(el => el.LastError)
            .HasColumnType("text");

        builder.Property(el => el.SentAt)
            .HasColumnType("timestamptz");

        builder.Property(el => el.TriggeredBy)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(el => el.CorrelationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(el => new { el.CorrelationId, el.ToAddress })
            .IsUnique();
        builder.HasIndex(el => new { el.Status, el.SentAt });
    }
}
