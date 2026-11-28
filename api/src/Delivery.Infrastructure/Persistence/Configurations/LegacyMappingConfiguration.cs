using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class LegacyMappingConfiguration : IEntityTypeConfiguration<LegacyMapping>
{
    public void Configure(EntityTypeBuilder<LegacyMapping> builder)
    {
        builder.ToTable("legacy_mappings");
        builder.HasKey(e => new { e.Entity, e.LegacyId });
        builder.Property(e => e.Entity).HasColumnName("entity").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.LegacyId).HasColumnName("legacyId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.NewId).HasColumnName("newId").IsRequired();
        builder.Property(e => e.Hash).HasColumnName("hash").IsRequired();
        builder.Property(e => e.ImportedAt).HasColumnName("importedAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.HasIndex(e => new { e.Entity, e.NewId });
    }
}
