using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> builder)
    {
        builder.ToTable("agencies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired();
        builder.Property(e => e.Phone).HasColumnName("phone");
        builder.Property(e => e.Email).HasColumnName("email");
        builder.Property(e => e.Address).HasColumnName("address");
        builder.Property(e => e.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
        builder.Property(e => e.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
        builder.Property(e => e.LogoUrl).HasColumnName("logoUrl");
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
        builder.Property(e => e.Timezone).HasColumnName("timezone");
        builder.Property(e => e.IsActive).HasColumnName("isActive").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => e.CityId);
        builder.HasOne(e => e.City).WithMany(e => e.Agencies).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
