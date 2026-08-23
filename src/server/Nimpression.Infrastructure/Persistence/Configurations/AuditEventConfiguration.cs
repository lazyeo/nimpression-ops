using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Standalone;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");

        builder.HasKey(ae => ae.Id);

        builder.Property(ae => ae.ActorUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ae => ae.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(ae => ae.ActorRole)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(ae => ae.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ae => ae.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ae => ae.EntityId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ae => ae.BeforeJson)
            .HasColumnType("text");

        builder.Property(ae => ae.AfterJson)
            .HasColumnType("text");

        builder.Property(ae => ae.IpAddress)
            .HasMaxLength(45);

        builder.Property(ae => ae.UserAgent)
            .HasMaxLength(500);

        builder.Property(ae => ae.OccurredAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(ae => new { ae.EntityType, ae.EntityId, ae.OccurredAt });
        builder.HasIndex(ae => new { ae.ActorUserId, ae.OccurredAt });
    }
}
