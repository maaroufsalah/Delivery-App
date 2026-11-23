using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class BlNoteConfiguration : IEntityTypeConfiguration<BlNote>
{
    public void Configure(EntityTypeBuilder<BlNote> builder)
    {
        builder.ToTable("bl_notes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.HandedOverById).HasColumnName("handedOverById");
        builder.Property(e => e.HandedOverAt).HasColumnName("handedOverAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReceivedById).HasColumnName("receivedById");
        builder.Property(e => e.ReceivedAt).HasColumnName("receivedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReturnedById).HasColumnName("returnedById");
        builder.Property(e => e.ReturnedAt).HasColumnName("returnedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReturnedToName).HasColumnName("returnedToName");
        builder.Property(e => e.ClosedById).HasColumnName("closedById");
        builder.Property(e => e.ClosedAt).HasColumnName("closedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.CourierId, e.Status });
        builder.HasIndex(e => new { e.ClientId, e.Status });
        builder.HasOne(e => e.Courier).WithMany(e => e.BlNotes).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Client).WithMany(e => e.BlNotes).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.BlNotes).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.CreatedBy).WithMany(e => e.CreatedBlNotes).HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
