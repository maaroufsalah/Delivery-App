using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class LabelJobConfiguration : IEntityTypeConfiguration<LabelJob>
{
    public void Configure(EntityTypeBuilder<LabelJob> builder)
    {
        builder.ToTable("label_jobs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.RequestedById).HasColumnName("requestedById").IsRequired();
        builder.Property(e => e.Format).HasColumnName("format").HasMaxLength(8).IsRequired();
        builder.Property(e => e.ParcelCount).HasColumnName("parcelCount").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.StorageKey).HasColumnName("storageKey");
        builder.Property(e => e.SizeBytes).HasColumnName("sizeBytes");
        builder.Property(e => e.Error).HasColumnName("error");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.StartedAt).HasColumnName("startedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.FinishedAt).HasColumnName("finishedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ExpiresAt).HasColumnName("expiresAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.HasIndex(e => new { e.RequestedById, e.CreatedAt });
        builder.HasIndex(e => e.ExpiresAt);
    }
}
