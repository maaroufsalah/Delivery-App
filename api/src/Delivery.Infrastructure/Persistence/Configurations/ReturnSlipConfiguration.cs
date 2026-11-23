using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ReturnSlipConfiguration : IEntityTypeConfiguration<ReturnSlip>
{
    public void Configure(EntityTypeBuilder<ReturnSlip> builder)
    {
        builder.ToTable("return_slips");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.DeliveredAt).HasColumnName("deliveredAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReceivedByName).HasColumnName("receivedByName");
        builder.Property(e => e.DeliveredById).HasColumnName("deliveredById");
        builder.Property(e => e.ProofName).HasColumnName("proofName");
        builder.Property(e => e.ProofMimeType).HasColumnName("proofMimeType");
        builder.Property(e => e.ProofPath).HasColumnName("proofPath");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.ClientId, e.Status });
        builder.HasIndex(e => new { e.AgencyId, e.CreatedAt });
        builder.HasOne(e => e.Client).WithMany(e => e.ReturnSlips).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.ReturnSlips).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.DeliveredBy).WithMany(e => e.DeliveredReturnSlips).HasForeignKey(e => e.DeliveredById).OnDelete(DeleteBehavior.SetNull);
    }
}
