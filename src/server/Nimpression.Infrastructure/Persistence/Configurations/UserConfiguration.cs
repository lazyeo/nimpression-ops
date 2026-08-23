using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasConversion(
                email => email.Value,
                value => new EmailAddress(value))
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.AvatarKey)
            .HasMaxLength(200);

        builder.Property(u => u.Locale)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(u => u.LastLoginAt)
            .HasColumnType("timestamptz");

        builder.Property(u => u.FailedLoginAttempts)
            .IsRequired();

        builder.Property(u => u.LockoutEnd)
            .HasColumnType("timestamptz");
    }
}
