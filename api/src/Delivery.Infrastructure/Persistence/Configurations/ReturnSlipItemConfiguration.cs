using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ReturnSlipItemConfiguration : IEntityTypeConfiguration<ReturnSlipItem>
{
    public void Configure(EntityTypeBuilder<ReturnSlipItem> builder)
    {
        builder.ToTable("return_slip_items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.SlipId).HasColumnName("slipId").IsRequired();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").HasConversion<string>().IsRequired();
        builder.Property(e => e.FeeApplied).HasColumnName("feeApplied").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.DeliveredParcelId).HasColumnName("deliveredParcelId");
        builder.HasIndex(e => e.DeliveredParcelId).IsUnique();
        builder.Property(e => e.ReservedParcelId).HasColumnName("reservedParcelId");
        builder.HasIndex(e => e.ReservedParcelId).IsUnique();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.SlipId, e.ParcelId }).IsUnique();
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.Slip).WithMany(e => e.Items).HasForeignKey(e => e.SlipId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.SlipItems).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
