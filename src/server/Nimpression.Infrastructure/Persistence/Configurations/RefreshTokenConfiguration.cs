using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Identity;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.UserId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(rt => rt.TokenHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(rt => rt.ExpiresAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(rt => rt.RevokedAt)
            .HasColumnType("timestamptz");

        builder.Property(rt => rt.ReplacedById);

        builder.Property(rt => rt.CreatedByIp)
            .HasMaxLength(45);

        builder.Property(rt => rt.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(rt => rt.UserId);
        builder.HasIndex(rt => rt.TokenHash);
    }
}
