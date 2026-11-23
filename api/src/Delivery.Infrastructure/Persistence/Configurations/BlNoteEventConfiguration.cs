using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class BlNoteEventConfiguration : IEntityTypeConfiguration<BlNoteEvent>
{
    public void Configure(EntityTypeBuilder<BlNoteEvent> builder)
    {
        builder.ToTable("bl_note_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.NoteId).HasColumnName("noteId").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Comment).HasColumnName("comment");
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.NoteId, e.CreatedAt });
        builder.HasOne(e => e.Note).WithMany(e => e.Events).HasForeignKey(e => e.NoteId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Author).WithMany(e => e.BlNoteEvents).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
