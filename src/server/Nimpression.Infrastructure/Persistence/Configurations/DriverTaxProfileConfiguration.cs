using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Services.Payroll;

namespace Nimpression.Infrastructure.Persistence.Configurations;

public sealed class DriverTaxProfileConfiguration : IEntityTypeConfiguration<DriverTaxProfile>
{
    public void Configure(EntityTypeBuilder<DriverTaxProfile> builder)
    {
        builder.ToTable("DriverTaxProfiles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
        builder.Property(p => p.Declaration).HasConversion(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<DriverTaxDeclaration>(json, (JsonSerializerOptions?)null)!)
            .HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.ApprovedEmployee).HasConversion(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<EmployeeSettlementProfile>(json, (JsonSerializerOptions?)null))
            .HasColumnType("jsonb");
        builder.Property(p => p.ApprovedContractor).HasConversion(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<ContractorSettlementProfile>(json, (JsonSerializerOptions?)null))
            .HasColumnType("jsonb");
        builder.HasOne<Driver>().WithMany().HasForeignKey(p => p.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.SubmittedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.DriverId).IsUnique().HasDatabaseName("UX_DriverTaxProfiles_Pending")
            .HasFilter("\"Status\" = 'Pending'");
        builder.HasIndex(p => new { p.DriverId, p.EffectiveFrom }).IsUnique().HasDatabaseName("UX_DriverTaxProfiles_ApprovedDate")
            .HasFilter("\"Status\" = 'Approved'");
        builder.HasIndex(p => new { p.SubmittedAt, p.Id });
    }
}
