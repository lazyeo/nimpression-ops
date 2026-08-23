using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Standalone;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> builder)
    {
        builder.ToTable("DataSubjectRequests");

        builder.HasKey(dsr => dsr.Id);

        builder.Property(dsr => dsr.SubjectUserId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(dsr => dsr.SubjectUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(dsr => dsr.Kind)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(dsr => dsr.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(dsr => dsr.RequestedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(dsr => dsr.CompletedAt)
            .HasColumnType("timestamptz");

        builder.Property(dsr => dsr.ExportKey)
            .HasMaxLength(255);

        builder.Property(dsr => dsr.RejectionReason)
            .HasColumnType("text");

        builder.HasIndex(dsr => new { dsr.SubjectUserId, dsr.Status });
    }
}
