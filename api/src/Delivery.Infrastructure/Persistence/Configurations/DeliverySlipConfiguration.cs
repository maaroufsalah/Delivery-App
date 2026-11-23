using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class DeliverySlipConfiguration : IEntityTypeConfiguration<DeliverySlip>
{
    public void Configure(EntityTypeBuilder<DeliverySlip> builder)
    {
        builder.ToTable("delivery_slips");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired();
        builder.HasIndex(e => e.ParcelId).IsUnique();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.CourierId).HasColumnName("courierId");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Reference).HasColumnName("reference");
        builder.Property(e => e.CollectedById).HasColumnName("collectedById");
        builder.Property(e => e.CollectedAt).HasColumnName("collectedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.HandedOverById).HasColumnName("handedOverById");
        builder.Property(e => e.HandedOverAt).HasColumnName("handedOverAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReceivedById).HasColumnName("receivedById");
        builder.Property(e => e.ReceivedAt).HasColumnName("receivedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReturnedById).HasColumnName("returnedById");
        builder.Property(e => e.ReturnedAt).HasColumnName("returnedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReturnedToName).HasColumnName("returnedToName");
        builder.Property(e => e.ClosedById).HasColumnName("closedById");
        builder.Property(e => e.ClosedAt).HasColumnName("closedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.ClientId, e.Status });
        builder.HasIndex(e => new { e.CourierId, e.Status });
        builder.HasOne(e => e.Parcel).WithOne(e => e.DeliverySlip).HasForeignKey<DeliverySlip>(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Client).WithMany(e => e.DeliverySlips).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Courier).WithMany(e => e.DeliverySlips).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict);
    }
}
