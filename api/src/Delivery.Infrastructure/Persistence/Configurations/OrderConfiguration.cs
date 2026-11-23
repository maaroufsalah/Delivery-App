using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").HasConversion<string>().IsRequired();
        builder.Property(e => e.ExternalRef).HasColumnName("externalRef");
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId");
        builder.Property(e => e.RecipientName).HasColumnName("recipientName").IsRequired();
        builder.Property(e => e.RecipientPhone).HasColumnName("recipientPhone").IsRequired();
        builder.Property(e => e.DestinationCityId).HasColumnName("destinationCityId").IsRequired();
        builder.Property(e => e.DestinationDistrictId).HasColumnName("destinationDistrictId");
        builder.Property(e => e.DestinationDistrictName).HasColumnName("destinationDistrictName").HasMaxLength(120);
        builder.Property(e => e.DeliveryAddress).HasColumnName("deliveryAddress").IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.TotalAmount).HasColumnName("totalAmount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId");
        builder.HasIndex(e => e.ParcelId).IsUnique();
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => new { e.ClientId, e.ExternalRef }).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.ClientId, e.CreatedAt });
        builder.HasOne(e => e.Client).WithMany(e => e.Orders).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.Orders).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DestinationCity).WithMany(e => e.Orders).HasForeignKey(e => e.DestinationCityId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.DestinationDistrict).WithMany(e => e.Orders).HasForeignKey(e => e.DestinationDistrictId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Parcel).WithOne(e => e.Order).HasForeignKey<Order>(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict);
    }
}
