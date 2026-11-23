using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ReturnNoteConfiguration : IEntityTypeConfiguration<ReturnNote>
{
    public void Configure(EntityTypeBuilder<ReturnNote> builder)
    {
        builder.ToTable("return_notes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.CourierId).HasColumnName("courierId");
        builder.Property(e => e.ValidatedAt).HasColumnName("validatedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ClientId);
        builder.HasOne(e => e.Client).WithMany(e => e.ReturnNotes).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Courier).WithMany(e => e.ReturnNotes).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict);
    }
}
