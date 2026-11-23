using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(e => new { e.Role, e.PermissionId });
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().IsRequired().ValueGeneratedNever();
        builder.Property(e => e.PermissionId).HasColumnName("permissionId").IsRequired().ValueGeneratedNever();
        builder.HasOne(e => e.Permission).WithMany(e => e.Roles).HasForeignKey(e => e.PermissionId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
