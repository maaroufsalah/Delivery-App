using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.Reference).HasColumnName("reference");
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.LengthMm).HasColumnName("lengthMm");
        builder.Property(e => e.WidthMm).HasColumnName("widthMm");
        builder.Property(e => e.HeightMm).HasColumnName("heightMm");
        builder.Property(e => e.WeightG).HasColumnName("weightG");
        builder.Property(e => e.Weight).HasColumnName("weight").HasPrecision(10, 3);
        builder.Property(e => e.Length).HasColumnName("length").HasPrecision(10, 2);
        builder.Property(e => e.Width).HasColumnName("width").HasPrecision(10, 2);
        builder.Property(e => e.Height).HasColumnName("height").HasPrecision(10, 2);
        builder.Property(e => e.Price).HasColumnName("price").HasPrecision(12, 2);
        builder.Property(e => e.Value).HasColumnName("value").HasPrecision(12, 2);
        builder.Property(e => e.ImageUrl).HasColumnName("imageUrl");
        builder.Property(e => e.ImageKey).HasColumnName("imageKey");
        builder.Property(e => e.InternalStock).HasColumnName("internalStock");
        builder.Property(e => e.IsActive).HasColumnName("isActive").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => e.Reference);
        builder.HasOne(e => e.Client).WithMany(e => e.Products).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
