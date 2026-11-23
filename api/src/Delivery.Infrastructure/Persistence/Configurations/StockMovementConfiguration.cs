using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.StockItemId).HasColumnName("stockItemId").IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("productId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
        builder.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(e => e.Balance).HasColumnName("balance").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.StorageRequestId).HasColumnName("storageRequestId");
        builder.Property(e => e.ParcelId).HasColumnName("parcelId");
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.StockItemId, e.CreatedAt });
        builder.HasIndex(e => new { e.ProductId, e.CreatedAt });
        builder.HasOne(e => e.StockItem).WithMany(e => e.Movements).HasForeignKey(e => e.StockItemId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Product).WithMany(e => e.Movements).HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.StockMovements).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.StorageRequest).WithMany(e => e.Movements).HasForeignKey(e => e.StorageRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Parcel).WithMany(e => e.StockMovements).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Author).WithMany(e => e.StockMovements).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
