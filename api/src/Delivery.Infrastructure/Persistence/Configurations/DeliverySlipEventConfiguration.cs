using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class DeliverySlipEventConfiguration : IEntityTypeConfiguration<DeliverySlipEvent>
{
    public void Configure(EntityTypeBuilder<DeliverySlipEvent> builder)
    {
        builder.ToTable("delivery_slip_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.SlipId).HasColumnName("slipId").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Comment).HasColumnName("comment");
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.SlipId, e.CreatedAt });
        builder.HasOne(e => e.Slip).WithMany(e => e.Events).HasForeignKey(e => e.SlipId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Author).WithMany(e => e.DeliverySlipEvents).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
