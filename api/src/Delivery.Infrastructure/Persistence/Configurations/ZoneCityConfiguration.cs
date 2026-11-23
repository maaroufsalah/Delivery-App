using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ZoneCityConfiguration : IEntityTypeConfiguration<ZoneCity>
{
    public void Configure(EntityTypeBuilder<ZoneCity> builder)
    {
        builder.ToTable("zone_cities");
        builder.HasKey(e => new { e.ZoneId, e.CityId });
        builder.Property(e => e.ZoneId).HasColumnName("zoneId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired().ValueGeneratedNever();
        builder.HasIndex(e => e.CityId);
        builder.HasOne(e => e.Zone).WithMany(e => e.CityLinks).HasForeignKey(e => e.ZoneId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.City).WithMany(e => e.ZoneLinks).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
