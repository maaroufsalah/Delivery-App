using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ReturnHandoverItemConfiguration : IEntityTypeConfiguration<ReturnHandoverItem>
{
    public void Configure(EntityTypeBuilder<ReturnHandoverItem> builder)
    {
        builder.ToTable("return_handover_items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.HandoverId).HasColumnName("handoverId").IsRequired();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.ConfirmedParcelId).HasColumnName("confirmedParcelId");
        builder.HasIndex(e => e.ConfirmedParcelId).IsUnique();
        builder.Property(e => e.ReservedParcelId).HasColumnName("reservedParcelId");
        builder.HasIndex(e => e.ReservedParcelId).IsUnique();
        builder.Property(e => e.CorrectedAt).HasColumnName("correctedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CorrectedStatusId).HasColumnName("correctedStatusId");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.HandoverId, e.ParcelId }).IsUnique();
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.Handover).WithMany(e => e.Items).HasForeignKey(e => e.HandoverId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.HandoverItems).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
