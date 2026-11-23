using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Entity).HasColumnName("entity").IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entityId").IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").HasConversion<string>().IsRequired();
        builder.Property(e => e.Before).HasColumnName("before").HasColumnType("jsonb");
        builder.Property(e => e.After).HasColumnName("after").HasColumnType("jsonb");
        builder.Property(e => e.Diff).HasColumnName("diff");
        builder.Property(e => e.SubjectClientId).HasColumnName("subjectClientId");
        builder.Property(e => e.UserId).HasColumnName("userId");
        builder.Property(e => e.UserEmail).HasColumnName("userEmail");
        builder.Property(e => e.Role).HasColumnName("role");
        builder.Property(e => e.Ip).HasColumnName("ip");
        builder.Property(e => e.UserAgent).HasColumnName("userAgent");
        builder.Property(e => e.RequestId).HasColumnName("requestId");
        builder.Property(e => e.Source).HasColumnName("source");
        builder.Property(e => e.At).HasColumnName("at").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => e.At);
        builder.HasIndex(e => new { e.Entity, e.EntityId, e.At });
        builder.HasIndex(e => new { e.UserId, e.At });
        builder.HasIndex(e => new { e.SubjectClientId, e.At });
        builder.HasOne(e => e.User).WithMany(e => e.AuditLogs).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
