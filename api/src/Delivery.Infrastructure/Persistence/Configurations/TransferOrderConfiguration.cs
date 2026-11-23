using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class TransferOrderConfiguration : IEntityTypeConfiguration<TransferOrder>
{
    public void Configure(EntityTypeBuilder<TransferOrder> builder)
    {
        builder.ToTable("transfer_orders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").HasConversion<string>().IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId");
        builder.Property(e => e.Lines).HasColumnName("lines").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.InvoiceCodes).HasColumnName("invoiceCodes");
        builder.Property(e => e.TotalCollected).HasColumnName("totalCollected").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalFees).HasColumnName("totalFees").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalDue).HasColumnName("totalDue").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.CreatedAt);
        builder.HasOne(e => e.Agency).WithMany(e => e.TransferOrders).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreatedBy).WithMany(e => e.TransferOrders).HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
