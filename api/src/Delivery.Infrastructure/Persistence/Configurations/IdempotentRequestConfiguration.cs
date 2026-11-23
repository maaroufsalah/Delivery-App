using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class IdempotentRequestConfiguration : IEntityTypeConfiguration<IdempotentRequest>
{
    public void Configure(EntityTypeBuilder<IdempotentRequest> builder)
    {
        builder.ToTable("idempotent_requests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("userId").IsRequired();
        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(80).IsRequired();
        builder.Property(e => e.Path).HasColumnName("path").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Response).HasColumnName("response").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamp(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.UserId, e.Key }).IsUnique();
        builder.HasIndex(e => e.CreatedAt);
    }
}
