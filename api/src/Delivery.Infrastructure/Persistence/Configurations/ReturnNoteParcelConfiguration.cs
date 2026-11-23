using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ReturnNoteParcelConfiguration : IEntityTypeConfiguration<ReturnNoteParcel>
{
    public void Configure(EntityTypeBuilder<ReturnNoteParcel> builder)
    {
        builder.ToTable("return_note_parcels");
        builder.HasKey(e => new { e.ReturnNoteId, e.ParcelId, e.Leg });
        builder.Property(e => e.ReturnNoteId).HasColumnName("returnNoteId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Leg).HasColumnName("leg").HasConversion<string>().IsRequired().ValueGeneratedNever();
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.ReturnNote).WithMany(e => e.Parcels).HasForeignKey(e => e.ReturnNoteId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.ReturnLinks).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
