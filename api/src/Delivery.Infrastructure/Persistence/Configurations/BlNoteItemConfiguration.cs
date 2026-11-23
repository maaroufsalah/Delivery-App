using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class BlNoteItemConfiguration : IEntityTypeConfiguration<BlNoteItem>
{
    public void Configure(EntityTypeBuilder<BlNoteItem> builder)
    {
        builder.ToTable("bl_note_items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.NoteId).HasColumnName("noteId").IsRequired();
        builder.Property(e => e.SlipId).HasColumnName("slipId").IsRequired();
        builder.Property(e => e.ReservedSlipId).HasColumnName("reservedSlipId");
        builder.HasIndex(e => e.ReservedSlipId).IsUnique();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.NoteId, e.SlipId }).IsUnique();
        builder.HasIndex(e => e.SlipId);
        builder.HasOne(e => e.Note).WithMany(e => e.Items).HasForeignKey(e => e.NoteId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Slip).WithMany(e => e.NoteItems).HasForeignKey(e => e.SlipId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
