using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Identity;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class NewsReadReceiptConfiguration : IEntityTypeConfiguration<NewsReadReceipt>
{
    public void Configure(EntityTypeBuilder<NewsReadReceipt> builder)
    {
        builder.ToTable("NewsReadReceipts");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.NewsPostId)
            .IsRequired();

        builder.HasOne<NewsPost>()
            .WithMany()
            .HasForeignKey(r => r.NewsPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.UserId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.ReadAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(r => new { r.NewsPostId, r.UserId })
            .IsUnique();
    }
}
