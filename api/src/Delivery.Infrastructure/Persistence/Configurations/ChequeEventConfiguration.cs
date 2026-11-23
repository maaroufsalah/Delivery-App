using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ChequeEventConfiguration : IEntityTypeConfiguration<ChequeEvent>
{
    public void Configure(EntityTypeBuilder<ChequeEvent> builder)
    {
        builder.ToTable("cheque_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ChequeId).HasColumnName("chequeId").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").IsRequired();
        builder.Property(e => e.Comment).HasColumnName("comment");
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.AuthorName).HasColumnName("authorName");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.ChequeId, e.CreatedAt });
        builder.HasOne(e => e.Cheque).WithMany(e => e.Events).HasForeignKey(e => e.ChequeId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Author).WithMany(e => e.ChequeEvents).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
