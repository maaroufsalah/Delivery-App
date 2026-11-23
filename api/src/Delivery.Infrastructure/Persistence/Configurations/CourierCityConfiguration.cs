using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CourierCityConfiguration : IEntityTypeConfiguration<CourierCity>
{
    public void Configure(EntityTypeBuilder<CourierCity> builder)
    {
        builder.ToTable("courier_cities");
        builder.HasKey(e => new { e.CourierId, e.CityId });
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.PickupFee).HasColumnName("pickupFee").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.DeliveryFee).HasColumnName("deliveryFee").HasPrecision(12, 2).IsRequired();
        builder.HasIndex(e => e.CityId);
        builder.HasOne(e => e.Courier).WithMany(e => e.Cities).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.City).WithMany(e => e.CourierCities).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
