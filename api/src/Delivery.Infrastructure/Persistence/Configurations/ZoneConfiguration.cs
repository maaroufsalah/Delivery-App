using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("zones");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.BaseRate).HasColumnName("baseRate").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.SameCityRate).HasColumnName("sameCityRate").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.DeliveryDays).HasColumnName("deliveryDays").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("isActive").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
    }
}
