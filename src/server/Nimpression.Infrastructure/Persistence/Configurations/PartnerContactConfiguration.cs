using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class PartnerContactConfiguration : IEntityTypeConfiguration<PartnerContact>
{
    public void Configure(EntityTypeBuilder<PartnerContact> builder)
    {
        builder.ToTable("PartnerContacts");

        builder.HasKey(pc => pc.Id);

        builder.Property(pc => pc.Kind)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(pc => pc.CompanyName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(pc => pc.Email)
            .HasConversion(
                email => email.Value,
                value => new EmailAddress(value))
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(pc => pc.Active)
            .IsRequired();

        builder.HasIndex(pc => pc.Kind);
    }
}
