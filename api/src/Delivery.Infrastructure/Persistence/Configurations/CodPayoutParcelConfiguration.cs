using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CodPayoutParcelConfiguration : IEntityTypeConfiguration<CodPayoutParcel>
{
    public void Configure(EntityTypeBuilder<CodPayoutParcel> builder)
    {
        builder.ToTable("cod_payout_parcels");
        builder.HasKey(e => new { e.PayoutId, e.ParcelId });
        builder.Property(e => e.PayoutId).HasColumnName("payoutId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.PaidOutParcelId).HasColumnName("paidOutParcelId");
        builder.HasIndex(e => e.PaidOutParcelId).IsUnique();
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.Payout).WithMany(e => e.Parcels).HasForeignKey(e => e.PayoutId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.CodPayouts).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
