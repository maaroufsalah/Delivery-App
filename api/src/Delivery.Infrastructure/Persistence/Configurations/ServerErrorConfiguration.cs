using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ServerErrorConfiguration : IEntityTypeConfiguration<ServerError>
{
    public void Configure(EntityTypeBuilder<ServerError> builder)
    {
        builder.ToTable("server_errors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.At).HasColumnName("at").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.Source).HasColumnName("source").IsRequired();
        builder.Property(e => e.RequestId).HasColumnName("requestId");
        builder.Property(e => e.Method).HasColumnName("method");
        builder.Property(e => e.Route).HasColumnName("route").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.Code).HasColumnName("code");
        builder.Property(e => e.Message).HasColumnName("message").IsRequired();
        builder.Property(e => e.Stack).HasColumnName("stack");
        builder.Property(e => e.DurationMs).HasColumnName("durationMs");
        builder.Property(e => e.UserId).HasColumnName("userId");
        builder.Property(e => e.Role).HasColumnName("role");
        builder.Property(e => e.TenantId).HasColumnName("tenantId");
        builder.Property(e => e.Ip).HasColumnName("ip");
        builder.HasIndex(e => e.At);
        builder.HasIndex(e => e.RequestId);
        builder.HasIndex(e => new { e.Route, e.At });
    }
}
