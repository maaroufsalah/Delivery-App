using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class AgencyCityConfiguration : IEntityTypeConfiguration<AgencyCity>
{
    public void Configure(EntityTypeBuilder<AgencyCity> builder)
    {
        builder.ToTable("agency_cities");
        builder.HasKey(e => new { e.AgencyId, e.CityId });
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired().ValueGeneratedNever();
        builder.HasIndex(e => e.CityId);
        builder.HasOne(e => e.Agency).WithMany(e => e.CityLinks).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.City).WithMany(e => e.AgencyCities).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
