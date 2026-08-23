using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Area;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Code)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(a => a.Code)
            .IsUnique();

        builder.Property(a => a.Description)
            .HasMaxLength(500);

        builder.Property(a => a.GeoJson)
            .HasColumnType("text");

        builder.Property(a => a.IsActive)
            .IsRequired();
    }
}
