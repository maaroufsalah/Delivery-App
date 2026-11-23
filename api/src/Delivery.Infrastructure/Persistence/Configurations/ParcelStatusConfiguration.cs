using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ParcelStatusConfiguration : IEntityTypeConfiguration<ParcelStatus>
{
    public void Configure(EntityTypeBuilder<ParcelStatus> builder)
    {
        builder.ToTable("parcel_statuses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Key).HasColumnName("key").IsRequired();
        builder.HasIndex(e => e.Key).IsUnique();
        builder.Property(e => e.Group).HasColumnName("group").HasConversion<string>().IsRequired();
        builder.Property(e => e.Color).HasColumnName("color").HasMaxLength(9).IsRequired();
        builder.Property(e => e.Icon).HasColumnName("icon").IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sortOrder").IsRequired();
    }
}
