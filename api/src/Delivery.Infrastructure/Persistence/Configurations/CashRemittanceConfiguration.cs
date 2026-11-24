using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CashRemittanceConfiguration : IEntityTypeConfiguration<CashRemittance>
{
    public void Configure(EntityTypeBuilder<CashRemittance> builder)
    {
        builder.ToTable("cash_remittances");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Origin).HasColumnName("origin").HasConversion<string>().IsRequired();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired();
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.ExpectedCod).HasColumnName("expectedCod").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.ExpectedCommission).HasColumnName("expectedCommission").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.ExpectedNet).HasColumnName("expectedNet").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.DeclaredAmount).HasColumnName("declaredAmount").HasPrecision(12, 2);
        builder.Property(e => e.VarianceReason).HasColumnName("varianceReason");
        builder.Property(e => e.TotalCash).HasColumnName("totalCash").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalCheque).HasColumnName("totalCheque").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Variance).HasColumnName("variance").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.SettlementMode).HasColumnName("settlementMode").HasConversion<string>().IsRequired();
        builder.Property(e => e.PayModel).HasColumnName("payModel").HasConversion<string>().IsRequired();
        builder.Property(e => e.ProofName).HasColumnName("proofName");
        builder.Property(e => e.ProofMimeType).HasColumnName("proofMimeType");
        builder.Property(e => e.ProofPath).HasColumnName("proofPath");
        builder.Property(e => e.ConfirmedById).HasColumnName("confirmedById");
        builder.Property(e => e.ConfirmedAt).HasColumnName("confirmedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CourierClosedAt).HasColumnName("courierClosedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CourierClosedById).HasColumnName("courierClosedById");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.CourierId, e.Status });
        // Filet de sécurité du verrou consultatif : un seul retour de fonds ouvert par livreur.
        builder.HasIndex(e => e.CourierId).IsUnique().HasFilter("\"status\" = 'DRAFT' AND \"courierClosedAt\" IS NULL").HasDatabaseName("cash_remittances_one_open_per_courier");
        builder.HasIndex(e => new { e.AgencyId, e.CreatedAt });
        builder.HasOne(e => e.City).WithMany(e => e.CashRemittances).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Courier).WithMany(e => e.CashRemittances).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.CashRemittances).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.ConfirmedBy).WithMany(e => e.ConfirmedRemittances).HasForeignKey(e => e.ConfirmedById).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.CourierClosedBy).WithMany(e => e.CourierClosedRemittances).HasForeignKey(e => e.CourierClosedById).OnDelete(DeleteBehavior.SetNull);
    }
}
