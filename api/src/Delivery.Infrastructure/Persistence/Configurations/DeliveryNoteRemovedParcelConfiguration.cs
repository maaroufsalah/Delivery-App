using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryNoteRemovedParcelConfiguration : IEntityTypeConfiguration<DeliveryNoteRemovedParcel>
{
    public void Configure(EntityTypeBuilder<DeliveryNoteRemovedParcel> builder)
    {
        builder.ToTable("delivery_note_removed_parcels");
        builder.HasKey(e => new { e.DeliveryNoteId, e.ParcelId });
        builder.Property(e => e.DeliveryNoteId).HasColumnName("deliveryNoteId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.RouteId).HasColumnName("routeId");
        builder.Property(e => e.RemovedAt).HasColumnName("removedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.RemovedBy).HasColumnName("removedBy");
        builder.Property(e => e.RestoredAt).HasColumnName("restoredAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.RestoredBy).HasColumnName("restoredBy");
        builder.HasIndex(e => e.ParcelId);
        builder.HasIndex(e => e.RouteId);
        builder.HasOne(e => e.DeliveryNote).WithMany(e => e.RemovedParcels).HasForeignKey(e => e.DeliveryNoteId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.NoteRemovals).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
