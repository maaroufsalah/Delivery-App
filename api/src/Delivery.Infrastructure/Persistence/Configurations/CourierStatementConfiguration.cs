using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CourierStatementConfiguration : IEntityTypeConfiguration<CourierStatement>
{
    public void Configure(EntityTypeBuilder<CourierStatement> builder)
    {
        builder.ToTable("courier_statements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.PeriodStart).HasColumnName("periodStart").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.PeriodEnd).HasColumnName("periodEnd").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.PeriodKind).HasColumnName("periodKind").HasConversion<string>().IsRequired();
        builder.Property(e => e.PayModel).HasColumnName("payModel").HasConversion<string>().IsRequired();
        builder.Property(e => e.SettlementMode).HasColumnName("settlementMode").HasConversion<string>().IsRequired();
        builder.Property(e => e.BaseAmount).HasColumnName("baseAmount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.CodCollected).HasColumnName("codCollected").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.CommissionsTotal).HasColumnName("commissionsTotal").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.CommissionsDeducted).HasColumnName("commissionsDeducted").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.VariableAmount).HasColumnName("variableAmount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalDue).HasColumnName("totalDue").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.ValidatedAt).HasColumnName("validatedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ValidatedById).HasColumnName("validatedById");
        builder.Property(e => e.PaidAt).HasColumnName("paidAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.PaidById).HasColumnName("paidById");
        builder.Property(e => e.PaymentMethod).HasColumnName("paymentMethod").HasConversion<string>().IsRequired();
        builder.Property(e => e.BankAccountId).HasColumnName("bankAccountId");
        builder.Property(e => e.Reference).HasColumnName("reference");
        builder.Property(e => e.ChequeNumber).HasColumnName("chequeNumber");
        builder.Property(e => e.ChequeBank).HasColumnName("chequeBank");
        builder.Property(e => e.ChequeDate).HasColumnName("chequeDate");
        builder.Property(e => e.ProofName).HasColumnName("proofName");
        builder.Property(e => e.ProofMimeType).HasColumnName("proofMimeType");
        builder.Property(e => e.ProofPath).HasColumnName("proofPath");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.CourierId, e.PeriodStart });
        builder.HasIndex(e => new { e.AgencyId, e.CreatedAt });
        builder.HasOne(e => e.Courier).WithMany(e => e.Statements).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.CourierStatements).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.ValidatedBy).WithMany(e => e.ValidatedStatements).HasForeignKey(e => e.ValidatedById).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.PaidBy).WithMany(e => e.PaidStatements).HasForeignKey(e => e.PaidById).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.BankAccount).WithMany(e => e.CourierStatements).HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
