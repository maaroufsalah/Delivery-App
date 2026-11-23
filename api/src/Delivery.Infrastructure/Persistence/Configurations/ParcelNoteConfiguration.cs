using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ParcelNoteConfiguration : IEntityTypeConfiguration<ParcelNote>
{
    public void Configure(EntityTypeBuilder<ParcelNote> builder)
    {
        builder.ToTable("parcel_notes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired();
        builder.Property(e => e.Body).HasColumnName("body").IsRequired();
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.AuthorName).HasColumnName("authorName").IsRequired();
        builder.Property(e => e.AttachmentName).HasColumnName("attachmentName");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.ParcelId, e.CreatedAt });
        builder.HasOne(e => e.Parcel).WithMany(e => e.Notes).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Author).WithMany(e => e.ParcelNotes).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
