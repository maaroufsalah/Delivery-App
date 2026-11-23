using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class StorageRequestItemConfiguration : IEntityTypeConfiguration<StorageRequestItem>
{
    public void Configure(EntityTypeBuilder<StorageRequestItem> builder)
    {
        builder.ToTable("storage_request_items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.RequestId).HasColumnName("requestId").IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("productId").IsRequired();
        builder.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.RequestId, e.ProductId }).IsUnique();
        builder.HasIndex(e => e.ProductId);
        builder.HasOne(e => e.Request).WithMany(e => e.Items).HasForeignKey(e => e.RequestId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Product).WithMany(e => e.StorageItems).HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
