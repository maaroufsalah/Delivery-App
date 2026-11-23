using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ParcelItemConfiguration : IEntityTypeConfiguration<ParcelItem>
{
    public void Configure(EntityTypeBuilder<ParcelItem> builder)
    {
        builder.ToTable("parcel_items");
        builder.HasKey(e => new { e.ParcelId, e.ProductId });
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ProductId).HasColumnName("productId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
        builder.HasIndex(e => e.ProductId);
        builder.HasOne(e => e.Parcel).WithMany(e => e.Items).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Product).WithMany(e => e.ParcelItems).HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
