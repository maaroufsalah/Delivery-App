using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class UserPermissionOverrideConfiguration : IEntityTypeConfiguration<UserPermissionOverride>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverride> builder)
    {
        builder.ToTable("user_permission_overrides");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("userId").IsRequired();
        builder.Property(e => e.PermissionId).HasColumnName("permissionId").IsRequired();
        builder.Property(e => e.Granted).HasColumnName("granted").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.HasIndex(e => new { e.UserId, e.PermissionId }).IsUnique();
        builder.HasOne(e => e.User).WithMany(e => e.PermissionOverrides).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Permission).WithMany(e => e.Overrides).HasForeignKey(e => e.PermissionId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
