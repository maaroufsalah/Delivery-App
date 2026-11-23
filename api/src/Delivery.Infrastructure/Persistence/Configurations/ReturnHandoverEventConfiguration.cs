using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ReturnHandoverEventConfiguration : IEntityTypeConfiguration<ReturnHandoverEvent>
{
    public void Configure(EntityTypeBuilder<ReturnHandoverEvent> builder)
    {
        builder.ToTable("return_handover_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.HandoverId).HasColumnName("handoverId").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Comment).HasColumnName("comment");
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.HandoverId, e.CreatedAt });
        builder.HasOne(e => e.Handover).WithMany(e => e.Events).HasForeignKey(e => e.HandoverId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Author).WithMany(e => e.ReturnHandoverEvents).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
