using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Identity;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class NewsPostConfiguration : IEntityTypeConfiguration<NewsPost>
{
    public void Configure(EntityTypeBuilder<NewsPost> builder)
    {
        builder.ToTable("NewsPosts");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.AuthorUserId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(n => n.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.BodyEn)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(n => n.BodyZh)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(n => n.Audience)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.PublishedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(n => n.Pinned)
            .IsRequired();

        builder.Property(n => n.IsActive)
            .IsRequired();

        builder.HasIndex(n => new { n.PublishedAt, n.IsActive });
        builder.HasIndex(n => n.AuthorUserId);
    }
}
