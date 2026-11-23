using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class FieldChangeConfiguration : IEntityTypeConfiguration<FieldChange>
{
    public void Configure(EntityTypeBuilder<FieldChange> builder)
    {
        builder.ToTable("field_changes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Entity).HasColumnName("entity").IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entityId").IsRequired();
        builder.Property(e => e.Field).HasColumnName("field").IsRequired();
        builder.Property(e => e.OldValue).HasColumnName("oldValue");
        builder.Property(e => e.NewValue).HasColumnName("newValue").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.AuthorName).HasColumnName("authorName").IsRequired();
        builder.Property(e => e.At).HasColumnName("at").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.Entity, e.EntityId, e.At });
        builder.HasIndex(e => e.At);
        builder.HasOne(e => e.Author).WithMany(e => e.FieldChanges).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
