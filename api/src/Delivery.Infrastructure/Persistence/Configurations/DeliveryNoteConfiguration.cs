using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryNoteConfiguration : IEntityTypeConfiguration<DeliveryNote>
{
    public void Configure(EntityTypeBuilder<DeliveryNote> builder)
    {
        builder.ToTable("delivery_notes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<string>().IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.LocationCityId).HasColumnName("locationCityId").IsRequired();
        builder.Property(e => e.ValidatedAt).HasColumnName("validatedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ValidatedBy).HasColumnName("validatedBy");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.ImportedAt).HasColumnName("importedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.PickedUpAt).HasColumnName("pickedUpAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.PickedUpBy).HasColumnName("pickedUpBy");
        builder.Property(e => e.PickupFeeApplied).HasColumnName("pickupFeeApplied").HasPrecision(12, 2);
        builder.Property(e => e.PickupThresholdApplied).HasColumnName("pickupThresholdApplied");
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => new { e.Type, e.Status });
        builder.HasIndex(e => new { e.AgencyId, e.CreatedAt });
        builder.HasIndex(e => e.ClientId);
        builder.HasOne(e => e.Client).WithMany(e => e.DeliveryNotes).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.DeliveryNotes).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.LocationCity).WithMany(e => e.DeliveryNotes).HasForeignKey(e => e.LocationCityId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
