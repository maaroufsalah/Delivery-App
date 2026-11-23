using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ClientDiscountCityConfiguration : IEntityTypeConfiguration<ClientDiscountCity>
{
    public void Configure(EntityTypeBuilder<ClientDiscountCity> builder)
    {
        builder.ToTable("client_discount_cities");
        builder.HasKey(e => new { e.DiscountId, e.CityId });
        builder.Property(e => e.DiscountId).HasColumnName("discountId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired().ValueGeneratedNever();
        builder.HasIndex(e => e.CityId);
        builder.HasOne(e => e.Discount).WithMany(e => e.Cities).HasForeignKey(e => e.DiscountId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.City).WithMany(e => e.DiscountLinks).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
