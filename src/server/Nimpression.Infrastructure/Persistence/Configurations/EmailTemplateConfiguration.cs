using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Communications;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");

        builder.HasKey(et => et.Id);

        builder.Property(et => et.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(et => et.Key)
            .IsUnique();

        builder.Property(et => et.SubjectEn)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(et => et.SubjectZh)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(et => et.BodyEn)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(et => et.BodyZh)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(et => et.Active)
            .IsRequired();
    }
}
