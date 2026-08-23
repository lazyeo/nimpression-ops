using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Standalone;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(om => om.Id);

        builder.Property(om => om.Type)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(om => om.PayloadJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(om => om.OccurredAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(om => om.ProcessedAt)
            .HasColumnType("timestamptz");

        builder.Property(om => om.Attempts)
            .IsRequired();

        builder.Property(om => om.Error)
            .HasColumnType("text");

        builder.HasIndex(om => new { om.ProcessedAt, om.OccurredAt });
    }
}
