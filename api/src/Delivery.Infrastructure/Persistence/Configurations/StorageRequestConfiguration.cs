using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class StorageRequestConfiguration : IEntityTypeConfiguration<StorageRequest>
{
    public void Configure(EntityTypeBuilder<StorageRequest> builder)
    {
        builder.ToTable("storage_requests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.RequestedAt).HasColumnName("requestedAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.DecidedAt).HasColumnName("decidedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.DecidedById).HasColumnName("decidedById");
        builder.Property(e => e.RefusalReason).HasColumnName("refusalReason");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.ClientId, e.Status });
        builder.HasOne(e => e.Client).WithMany(e => e.StorageRequests).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.StorageRequests).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.DecidedBy).WithMany(e => e.DecidedStorageRequests).HasForeignKey(e => e.DecidedById).OnDelete(DeleteBehavior.SetNull);
    }
}
