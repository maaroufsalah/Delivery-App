using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ChequeConfiguration : IEntityTypeConfiguration<Cheque>
{
    public void Configure(EntityTypeBuilder<Cheque> builder)
    {
        builder.ToTable("cheques");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Number).HasColumnName("number").IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Bank).HasColumnName("bank");
        builder.Property(e => e.ChequeDate).HasColumnName("chequeDate");
        builder.Property(e => e.ReceivedAt).HasColumnName("receivedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.PayerName).HasColumnName("payerName").IsRequired();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired();
        builder.HasIndex(e => e.ParcelId).IsUnique();
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired();
        builder.Property(e => e.SessionId).HasColumnName("sessionId");
        builder.Property(e => e.RemittanceId).HasColumnName("remittanceId");
        builder.Property(e => e.InvoiceId).HasColumnName("invoiceId");
        builder.Property(e => e.HandedOverAt).HasColumnName("handedOverAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReceivedByAdminAt).HasColumnName("receivedByAdminAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.TransmittedAt).HasColumnName("transmittedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.MerchantReceivedAt).HasColumnName("merchantReceivedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.RejectedAt).HasColumnName("rejectedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CancelledAt).HasColumnName("cancelledAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.DepositedAt).HasColumnName("depositedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CashedAt).HasColumnName("cashedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.RejectionReason).HasColumnName("rejectionReason");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.CourierId, e.ReceivedAt });
        builder.HasIndex(e => e.Bank);
        builder.HasOne(e => e.Parcel).WithOne(e => e.Cheque).HasForeignKey<Cheque>(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Courier).WithMany(e => e.Cheques).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Session).WithMany(e => e.Cheques).HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Remittance).WithMany(e => e.Cheques).HasForeignKey(e => e.RemittanceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Invoice).WithMany(e => e.Cheques).HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.SetNull);
    }
}
