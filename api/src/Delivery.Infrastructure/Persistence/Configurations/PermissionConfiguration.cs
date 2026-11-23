using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Key).HasColumnName("key").IsRequired();
        builder.HasIndex(e => e.Key).IsUnique();
        builder.Property(e => e.Label).HasColumnName("label").IsRequired();
        builder.Property(e => e.Description).HasColumnName("description");
    }
}
