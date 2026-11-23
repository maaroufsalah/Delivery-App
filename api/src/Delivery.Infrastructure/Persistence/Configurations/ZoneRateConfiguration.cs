using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ZoneRateConfiguration : IEntityTypeConfiguration<ZoneRate>
{
    public void Configure(EntityTypeBuilder<ZoneRate> builder)
    {
        builder.ToTable("zone_rates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.FromZoneId).HasColumnName("fromZoneId").IsRequired();
        builder.Property(e => e.ToZoneId).HasColumnName("toZoneId").IsRequired();
        builder.Property(e => e.Rate).HasColumnName("rate").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.DeliveryDays).HasColumnName("deliveryDays").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("isActive").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => new { e.FromZoneId, e.ToZoneId }).IsUnique();
        builder.HasOne(e => e.FromZone).WithMany(e => e.RatesFrom).HasForeignKey(e => e.FromZoneId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.ToZone).WithMany(e => e.RatesTo).HasForeignKey(e => e.ToZoneId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
