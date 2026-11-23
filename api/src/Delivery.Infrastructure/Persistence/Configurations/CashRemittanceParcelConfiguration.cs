using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CashRemittanceParcelConfiguration : IEntityTypeConfiguration<CashRemittanceParcel>
{
    public void Configure(EntityTypeBuilder<CashRemittanceParcel> builder)
    {
        builder.ToTable("cash_remittance_parcels");
        builder.HasKey(e => new { e.RemittanceId, e.ParcelId });
        builder.Property(e => e.RemittanceId).HasColumnName("remittanceId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Cash).HasColumnName("cash").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Cheque).HasColumnName("cheque").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Commission).HasColumnName("commission").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.ConfirmedParcelId).HasColumnName("confirmedParcelId");
        builder.HasIndex(e => e.ConfirmedParcelId).IsUnique();
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.Remittance).WithMany(e => e.Parcels).HasForeignKey(e => e.RemittanceId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.Remittances).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
