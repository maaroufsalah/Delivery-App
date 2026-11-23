using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class PettyCashMovementConfiguration : IEntityTypeConfiguration<PettyCashMovement>
{
    public void Configure(EntityTypeBuilder<PettyCashMovement> builder)
    {
        builder.ToTable("petty_cash_movements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurredAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Label).HasColumnName("label");
        builder.Property(e => e.Category).HasColumnName("category");
        builder.Property(e => e.BuyerId).HasColumnName("buyerId");
        builder.Property(e => e.BuyerName).HasColumnName("buyerName");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.ProofName).HasColumnName("proofName");
        builder.Property(e => e.ProofMimeType).HasColumnName("proofMimeType");
        builder.Property(e => e.ProofPath).HasColumnName("proofPath");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.RefusalReason).HasColumnName("refusalReason");
        builder.Property(e => e.DecidedById).HasColumnName("decidedById");
        builder.Property(e => e.DecidedAt).HasColumnName("decidedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => new { e.AgencyId, e.OccurredAt });
        builder.HasOne(e => e.Agency).WithMany(e => e.PettyCashMovements).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Buyer).WithMany(e => e.PettyCashPurchases).HasForeignKey(e => e.BuyerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.CreatedBy).WithMany(e => e.PettyCashEntries).HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.DecidedBy).WithMany(e => e.PettyCashDecisions).HasForeignKey(e => e.DecidedById).OnDelete(DeleteBehavior.SetNull);
    }
}
