using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class PlatformAuditLogConfiguration : IEntityTypeConfiguration<PlatformAuditLog>
{
    public void Configure(EntityTypeBuilder<PlatformAuditLog> builder)
    {
        builder.ToTable("platform_audit_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ActorId).HasColumnName("actorId");
        builder.Property(e => e.ActorName).HasColumnName("actorName").IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").IsRequired();
        builder.Property(e => e.TargetCompanyId).HasColumnName("targetCompanyId");
        builder.Property(e => e.TargetCompanyName).HasColumnName("targetCompanyName").IsRequired();
        builder.Property(e => e.Details).HasColumnName("details").HasColumnType("jsonb");
        builder.Property(e => e.Ip).HasColumnName("ip");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => new { e.TargetCompanyId, e.CreatedAt });
        builder.HasOne(e => e.Actor).WithMany(e => e.PlatformActions).HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.SetNull);
    }
}
