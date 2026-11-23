using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class PricingRuleConfiguration : IEntityTypeConfiguration<PricingRule>
{
    public void Configure(EntityTypeBuilder<PricingRule> builder)
    {
        builder.ToTable("pricing_rules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.OriginCityId).HasColumnName("originCityId").IsRequired();
        builder.Property(e => e.DestinationCityId).HasColumnName("destinationCityId").IsRequired();
        builder.Property(e => e.RateDistrict).HasColumnName("rateDistrict").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.RateAgency).HasColumnName("rateAgency").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.RateRelayPoint).HasColumnName("rateRelayPoint").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.IsEnabled).HasColumnName("isEnabled").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => new { e.OriginCityId, e.DestinationCityId }).IsUnique();
        builder.HasOne(e => e.OriginCity).WithMany(e => e.OriginRules).HasForeignKey(e => e.OriginCityId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.DestinationCity).WithMany(e => e.DestRules).HasForeignKey(e => e.DestinationCityId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
