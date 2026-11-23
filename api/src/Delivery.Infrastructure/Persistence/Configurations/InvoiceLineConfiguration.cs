using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("invoice_lines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.InvoiceId).HasColumnName("invoiceId").IsRequired();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired();
        builder.Property(e => e.ParcelCode).HasColumnName("parcelCode").IsRequired();
        builder.Property(e => e.StatusId).HasColumnName("statusId").IsRequired();
        builder.Property(e => e.CodAmount).HasColumnName("codAmount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.DeliveryFee).HasColumnName("deliveryFee").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.FailureFee).HasColumnName("failureFee").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.VatAmount).HasColumnName("vatAmount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.ChequePaid).HasColumnName("chequePaid").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.NetDue).HasColumnName("netDue").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.InvoicedParcelId).HasColumnName("invoicedParcelId");
        builder.HasIndex(e => e.InvoicedParcelId).IsUnique();
        builder.HasIndex(e => new { e.InvoiceId, e.ParcelId }).IsUnique();
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.Invoice).WithMany(e => e.Lines).HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.InvoiceLines).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
