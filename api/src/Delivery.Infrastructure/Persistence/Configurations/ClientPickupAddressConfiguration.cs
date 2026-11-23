using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ClientPickupAddressConfiguration : IEntityTypeConfiguration<ClientPickupAddress>
{
    public void Configure(EntityTypeBuilder<ClientPickupAddress> builder)
    {
        builder.ToTable("client_pickup_addresses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.Label).HasColumnName("label").IsRequired();
        builder.Property(e => e.Address).HasColumnName("address").IsRequired();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired();
        builder.Property(e => e.DistrictId).HasColumnName("districtId");
        builder.Property(e => e.Phone).HasColumnName("phone");
        builder.Property(e => e.IsDefault).HasColumnName("isDefault").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => e.ClientId);
        builder.HasOne(e => e.Client).WithMany(e => e.PickupAddresses).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.City).WithMany(e => e.PickupAddresses).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.District).WithMany(e => e.PickupAddresses).HasForeignKey(e => e.DistrictId).OnDelete(DeleteBehavior.Restrict);
    }
}
