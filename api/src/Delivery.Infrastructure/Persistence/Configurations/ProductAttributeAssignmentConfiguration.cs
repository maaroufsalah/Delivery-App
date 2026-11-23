using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ProductAttributeAssignmentConfiguration : IEntityTypeConfiguration<ProductAttributeAssignment>
{
    public void Configure(EntityTypeBuilder<ProductAttributeAssignment> builder)
    {
        builder.ToTable("product_attribute_assignments");
        builder.HasKey(e => new { e.ProductId, e.ValueId });
        builder.Property(e => e.ProductId).HasColumnName("productId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ValueId).HasColumnName("valueId").IsRequired().ValueGeneratedNever();
        builder.HasIndex(e => e.ValueId);
        builder.HasOne(e => e.Product).WithMany(e => e.AttributeValues).HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Value).WithMany(e => e.Assignments).HasForeignKey(e => e.ValueId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
