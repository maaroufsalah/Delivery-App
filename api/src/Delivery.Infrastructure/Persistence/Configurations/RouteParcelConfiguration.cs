using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class RouteParcelConfiguration : IEntityTypeConfiguration<RouteParcel>
{
    public void Configure(EntityTypeBuilder<RouteParcel> builder)
    {
        builder.ToTable("route_parcels");
        builder.HasKey(e => new { e.RouteId, e.ParcelId });
        builder.Property(e => e.RouteId).HasColumnName("routeId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Sequence).HasColumnName("sequence").IsRequired();
        builder.Property(e => e.Confirmed).HasColumnName("confirmed").IsRequired();
        builder.Property(e => e.ConfirmedAt).HasColumnName("confirmedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ConfirmedBy).HasColumnName("confirmedBy");
        builder.Property(e => e.CourierFee).HasColumnName("courierFee").HasPrecision(12, 2);
        builder.Property(e => e.FinalStatusId).HasColumnName("finalStatusId");
        builder.Property(e => e.CodCollected).HasColumnName("codCollected").HasPrecision(12, 2);
        builder.Property(e => e.CodCash).HasColumnName("codCash").HasPrecision(12, 2);
        builder.Property(e => e.CodCheque).HasColumnName("codCheque").HasPrecision(12, 2);
        builder.Property(e => e.CourierCommission).HasColumnName("courierCommission").HasPrecision(12, 2);
        builder.Property(e => e.CourierCommissionSource).HasColumnName("courierCommissionSource").HasConversion<string>();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.CorrectedAt).HasColumnName("correctedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CorrectedStatusId).HasColumnName("correctedStatusId");
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.Route).WithMany(e => e.Parcels).HasForeignKey(e => e.RouteId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.RouteLinks).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
