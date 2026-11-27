using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceAdjustmentConfiguration : IEntityTypeConfiguration<InvoiceAdjustment>
{
    public void Configure(EntityTypeBuilder<InvoiceAdjustment> builder)
    {
        builder.ToTable("invoice_adjustments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.InvoiceId).HasColumnName("invoiceId").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.VatAmount).HasColumnName("vatAmount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").IsRequired();
        builder.Property(e => e.Reference).HasColumnName("reference");
        builder.Property(e => e.DeliveryNoteId).HasColumnName("deliveryNoteId");
        builder.HasIndex(e => e.DeliveryNoteId).IsUnique();
        builder.Property(e => e.CarriedFromInvoiceId).HasColumnName("carriedFromInvoiceId");
        builder.HasIndex(e => e.CarriedFromInvoiceId).IsUnique();
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedByName).HasColumnName("createdByName");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => e.InvoiceId);
        builder.HasIndex(e => e.Kind);
        builder.HasOne(e => e.Invoice).WithMany(e => e.Adjustments).HasForeignKey(e => e.InvoiceId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.CarriedFromInvoice).WithOne(e => e.CarryOverLine).HasForeignKey<InvoiceAdjustment>(e => e.CarriedFromInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DeliveryNote).WithOne(e => e.PickupFeeLine).HasForeignKey<InvoiceAdjustment>(e => e.DeliveryNoteId).OnDelete(DeleteBehavior.SetNull);
    }
}
