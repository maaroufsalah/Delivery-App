using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CourierCashSessionParcelConfiguration : IEntityTypeConfiguration<CourierCashSessionParcel>
{
    public void Configure(EntityTypeBuilder<CourierCashSessionParcel> builder)
    {
        builder.ToTable("courier_cash_session_parcels");
        builder.HasKey(e => new { e.SessionId, e.ParcelId });
        builder.Property(e => e.SessionId).HasColumnName("sessionId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Cash).HasColumnName("cash").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Cheque).HasColumnName("cheque").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Collected).HasColumnName("collected").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Commission).HasColumnName("commission").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.AddedAt).HasColumnName("addedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.SettledParcelId).HasColumnName("settledParcelId");
        builder.HasIndex(e => e.SettledParcelId).IsUnique();
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.Session).WithMany(e => e.Parcels).HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.CashSessionLinks).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
