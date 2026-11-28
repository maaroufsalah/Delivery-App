using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.TotalCod).HasColumnName("totalCod").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalDeliveryFee).HasColumnName("totalDeliveryFee").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalFailureFee).HasColumnName("totalFailureFee").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalVat).HasColumnName("totalVat").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalAdjustment).HasColumnName("totalAdjustment").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalDue).HasColumnName("totalDue").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.VatRate).HasColumnName("vatRate").IsRequired();
        builder.Property(e => e.ProVatRate).HasColumnName("proVatRate");
        builder.Property(e => e.BillingType).HasColumnName("billingType").HasConversion<string>().IsRequired();
        builder.Property(e => e.PeriodStart).HasColumnName("periodStart").HasColumnType("timestamptz(3)");
        builder.Property(e => e.PeriodEnd).HasColumnName("periodEnd").HasColumnType("timestamptz(3)");
        builder.Property(e => e.IssuedAt).HasColumnName("issuedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.IssuedById).HasColumnName("issuedById");
        builder.Property(e => e.DueAt).HasColumnName("dueAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.PaidAt).HasColumnName("paidAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.PaidById).HasColumnName("paidById");
        builder.Property(e => e.CarriedIntoId).HasColumnName("carriedIntoId");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.ImportedAt).HasColumnName("importedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.ClientId, e.CreatedAt });
        builder.HasIndex(e => new { e.AgencyId, e.CreatedAt });
        builder.HasOne(e => e.Client).WithMany(e => e.Invoices).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.Invoices).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.IssuedBy).WithMany(e => e.IssuedInvoices).HasForeignKey(e => e.IssuedById).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.CarriedInto).WithMany().HasForeignKey(e => e.CarriedIntoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PaidBy).WithMany(e => e.PaidInvoices).HasForeignKey(e => e.PaidById).OnDelete(DeleteBehavior.SetNull);
    }
}
