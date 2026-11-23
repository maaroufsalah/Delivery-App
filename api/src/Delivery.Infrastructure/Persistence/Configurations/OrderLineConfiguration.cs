using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.OrderId).HasColumnName("orderId").IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("productId").IsRequired();
        builder.Property(e => e.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(e => e.UnitPrice).HasColumnName("unitPrice").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.OrderId, e.ProductId }).IsUnique();
        builder.HasIndex(e => e.ProductId);
        builder.HasOne(e => e.Order).WithMany(e => e.Lines).HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Product).WithMany(e => e.OrderLines).HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
