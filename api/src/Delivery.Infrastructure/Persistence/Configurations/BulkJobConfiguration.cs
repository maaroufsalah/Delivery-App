using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class BulkJobConfiguration : IEntityTypeConfiguration<BulkJob>
{
    public void Configure(EntityTypeBuilder<BulkJob> builder)
    {
        builder.ToTable("bulk_jobs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.RequestedById).HasColumnName("requestedById").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").HasMaxLength(32).IsRequired();
        builder.Property(e => e.Target).HasColumnName("target").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Input).HasColumnName("input").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Scope).HasColumnName("scope").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Actor).HasColumnName("actor").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Total).HasColumnName("total").IsRequired();
        builder.Property(e => e.Processed).HasColumnName("processed").IsRequired();
        builder.Property(e => e.DoneIds).HasColumnName("doneIds").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Failures).HasColumnName("failures").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Result).HasColumnName("result").HasColumnType("jsonb");
        builder.Property(e => e.Error).HasColumnName("error");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.StartedAt).HasColumnName("startedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.FinishedAt).HasColumnName("finishedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => new { e.Target, e.Status });
        builder.HasIndex(e => e.Status);
    }
}
