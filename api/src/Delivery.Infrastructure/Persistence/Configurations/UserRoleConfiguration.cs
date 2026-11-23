using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(e => new { e.UserId, e.Role });
        builder.Property(e => e.UserId).HasColumnName("userId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().IsRequired().ValueGeneratedNever();
        builder.HasOne(e => e.User).WithMany(e => e.Roles).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
