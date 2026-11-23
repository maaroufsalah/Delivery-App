using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("refresh_sessions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("userId").IsRequired();
        builder.Property(e => e.FamilyId).HasColumnName("familyId").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").IsRequired();
        builder.Property(e => e.IssuedAt).HasColumnName("issuedAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.AccessExpiresAt).HasColumnName("accessExpiresAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expiresAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.RotatedAt).HasColumnName("rotatedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReplacedById).HasColumnName("replacedById");
        builder.Property(e => e.RevokedAt).HasColumnName("revokedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.FamilyId);
        builder.HasIndex(e => e.ExpiresAt);
    }
}
