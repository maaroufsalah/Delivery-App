using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ExchangeConfiguration : IEntityTypeConfiguration<Exchange>
{
    public void Configure(EntityTypeBuilder<Exchange> builder)
    {
        builder.ToTable("exchanges");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.OriginalParcelId).HasColumnName("originalParcelId").IsRequired();
        builder.Property(e => e.NewParcelId).HasColumnName("newParcelId");
        builder.HasIndex(e => e.NewParcelId).IsUnique();
        builder.Property(e => e.ReturnedProduct).HasColumnName("returnedProduct").IsRequired();
        builder.Property(e => e.NewProduct).HasColumnName("newProduct").IsRequired();
        builder.Property(e => e.OldDestination).HasColumnName("oldDestination").IsRequired();
        builder.Property(e => e.NewDestination).HasColumnName("newDestination").IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Fees).HasColumnName("fees").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.CourierId).HasColumnName("courierId");
        builder.Property(e => e.RequestedAt).HasColumnName("requestedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.PickedUpAt).HasColumnName("pickedUpAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.DeliveredAt).HasColumnName("deliveredAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ClosedAt).HasColumnName("closedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CancelledAt).HasColumnName("cancelledAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.OriginalParcelId);
        builder.HasOne(e => e.OriginalParcel).WithMany(e => e.ExchangesAsOriginal).HasForeignKey(e => e.OriginalParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.NewParcel).WithOne(e => e.ExchangeAsNew).HasForeignKey<Exchange>(e => e.NewParcelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Courier).WithMany(e => e.Exchanges).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreatedBy).WithMany(e => e.CreatedExchanges).HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
