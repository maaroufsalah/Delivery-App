using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class TransferOrderParcelConfiguration : IEntityTypeConfiguration<TransferOrderParcel>
{
    public void Configure(EntityTypeBuilder<TransferOrderParcel> builder)
    {
        builder.ToTable("transfer_order_parcels");
        builder.HasKey(e => new { e.TransferOrderId, e.ParcelId });
        builder.Property(e => e.TransferOrderId).HasColumnName("transferOrderId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired().ValueGeneratedNever();
        builder.HasIndex(e => e.ParcelId).IsUnique();
        builder.HasOne(e => e.TransferOrder).WithMany(e => e.Parcels).HasForeignKey(e => e.TransferOrderId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.TransferOrders).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
