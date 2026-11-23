using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryNoteParcelConfiguration : IEntityTypeConfiguration<DeliveryNoteParcel>
{
    public void Configure(EntityTypeBuilder<DeliveryNoteParcel> builder)
    {
        builder.ToTable("delivery_note_parcels");
        builder.HasKey(e => new { e.DeliveryNoteId, e.ParcelId });
        builder.Property(e => e.DeliveryNoteId).HasColumnName("deliveryNoteId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.CorrectedAt).HasColumnName("correctedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CorrectedStatusId).HasColumnName("correctedStatusId");
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.DeliveryNote).WithMany(e => e.Parcels).HasForeignKey(e => e.DeliveryNoteId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.NoteLinks).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
