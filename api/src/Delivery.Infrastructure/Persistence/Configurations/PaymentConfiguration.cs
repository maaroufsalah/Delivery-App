using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.InvoiceId).HasColumnName("invoiceId");
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Method).HasColumnName("method").HasConversion<string>().IsRequired();
        builder.Property(e => e.BankAccountId).HasColumnName("bankAccountId");
        builder.Property(e => e.Reference).HasColumnName("reference");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.PaidAt).HasColumnName("paidAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.ProofName).HasColumnName("proofName");
        builder.Property(e => e.ProofMimeType).HasColumnName("proofMimeType");
        builder.Property(e => e.ProofPath).HasColumnName("proofPath");
        builder.Property(e => e.ChequeNumber).HasColumnName("chequeNumber");
        builder.Property(e => e.ChequeBank).HasColumnName("chequeBank");
        builder.Property(e => e.ChequeDate).HasColumnName("chequeDate");
        builder.Property(e => e.CancelledAt).HasColumnName("cancelledAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CancelledById).HasColumnName("cancelledById");
        builder.Property(e => e.CancelReason).HasColumnName("cancelReason");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => new { e.ClientId, e.PaidAt });
        builder.HasIndex(e => new { e.AgencyId, e.PaidAt });
        builder.HasIndex(e => e.InvoiceId);
        // Un règlement ANNULÉ reste en base (et au journal) mais n'est plus un règlement : hors de toutes les sommes et listes.
        builder.HasQueryFilter(e => e.CancelledAt == null);
        builder.HasOne(e => e.Invoice).WithMany(e => e.Payments).HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Client).WithMany(e => e.Payments).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.Payments).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.BankAccount).WithMany(e => e.Payments).HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
