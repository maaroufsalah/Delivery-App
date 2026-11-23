using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ProviderTokenConfiguration : IEntityTypeConfiguration<ProviderToken>
{
    public void Configure(EntityTypeBuilder<ProviderToken> builder)
    {
        builder.ToTable("provider_tokens");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("userId").IsRequired();
        builder.Property(e => e.TokenHash).HasColumnName("tokenHash").HasMaxLength(64).IsRequired();
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.Property(e => e.TokenPrefix).HasColumnName("tokenPrefix").HasMaxLength(8).IsRequired();
        builder.Property(e => e.PreviousTokenHash).HasColumnName("previousTokenHash").HasMaxLength(64);
        builder.HasIndex(e => e.PreviousTokenHash).IsUnique();
        builder.Property(e => e.PreviousExpiresAt).HasColumnName("previousExpiresAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.Mode).HasColumnName("mode").HasConversion<string>().IsRequired();
        builder.Property(e => e.StatusMap).HasColumnName("statusMap").HasColumnType("jsonb");
        builder.Property(e => e.RateLimitPerMinute).HasColumnName("rateLimitPerMinute").IsRequired();
        builder.Property(e => e.RevokedAt).HasColumnName("revokedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.RotatedAt).HasColumnName("rotatedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.LastUsedAt).HasColumnName("lastUsedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasOne(e => e.Courier).WithMany(e => e.ProviderTokens).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.User).WithMany(e => e.ProviderTokens).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
