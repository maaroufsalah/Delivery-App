using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.IsPlatformAdmin).HasColumnName("isPlatformAdmin").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId");
        builder.Property(e => e.Email).HasColumnName("email");
        builder.Property(e => e.Username).HasColumnName("username");
        builder.Property(e => e.PasswordHash).HasColumnName("passwordHash").IsRequired();
        builder.Property(e => e.FirstName).HasColumnName("firstName").IsRequired();
        builder.Property(e => e.LastName).HasColumnName("lastName").IsRequired();
        builder.Property(e => e.Phone).HasColumnName("phone");
        builder.Property(e => e.AvatarUrl).HasColumnName("avatarUrl");
        builder.Property(e => e.JobTitle).HasColumnName("jobTitle");
        builder.Property(e => e.NationalId).HasColumnName("nationalId");
        builder.Property(e => e.IsActive).HasColumnName("isActive").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.TokensValidFrom).HasColumnName("tokensValidFrom").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.MustChangePassword).HasColumnName("mustChangePassword").IsRequired();
        builder.Property(e => e.InvitedAt).HasColumnName("invitedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.InvitedById).HasColumnName("invitedById");
        builder.Property(e => e.LastLoginAt).HasColumnName("lastLoginAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => e.Email).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasOne(e => e.Agency).WithMany(e => e.Users).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.InvitedBy).WithMany(e => e.InvitedUsers).HasForeignKey(e => e.InvitedById).OnDelete(DeleteBehavior.SetNull);
    }
}
