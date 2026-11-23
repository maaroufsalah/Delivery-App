using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ReturnHandoverConfiguration : IEntityTypeConfiguration<ReturnHandover>
{
    public void Configure(EntityTypeBuilder<ReturnHandover> builder)
    {
        builder.ToTable("return_handovers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.Origin).HasColumnName("origin").HasConversion<string>().IsRequired();
        builder.Property(e => e.CourierCanEdit).HasColumnName("courierCanEdit").IsRequired();
        builder.Property(e => e.PickedUpById).HasColumnName("pickedUpById");
        builder.Property(e => e.PickedUpAt).HasColumnName("pickedUpAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.HandedOverById).HasColumnName("handedOverById");
        builder.Property(e => e.HandedOverAt).HasColumnName("handedOverAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ConfirmedById).HasColumnName("confirmedById");
        builder.Property(e => e.ConfirmedAt).HasColumnName("confirmedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ClosedById).HasColumnName("closedById");
        builder.Property(e => e.ClosedAt).HasColumnName("closedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CancelledById).HasColumnName("cancelledById");
        builder.Property(e => e.CancelledAt).HasColumnName("cancelledAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CancelReason).HasColumnName("cancelReason");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.CourierId, e.Status });
        builder.HasIndex(e => new { e.AgencyId, e.CreatedAt });
        builder.HasOne(e => e.Courier).WithMany(e => e.ReturnHandovers).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.ReturnHandovers).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.ConfirmedBy).WithMany(e => e.ConfirmedReturnHandovers).HasForeignKey(e => e.ConfirmedById).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.CreatedBy).WithMany(e => e.CreatedReturnHandovers).HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
