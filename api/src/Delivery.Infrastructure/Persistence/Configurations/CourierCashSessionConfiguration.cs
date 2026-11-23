using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CourierCashSessionConfiguration : IEntityTypeConfiguration<CourierCashSession>
{
    public void Configure(EntityTypeBuilder<CourierCashSession> builder)
    {
        builder.ToTable("courier_cash_sessions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Day).HasColumnName("day").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.TotalCash).HasColumnName("totalCash").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalCheque).HasColumnName("totalCheque").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalCollected).HasColumnName("totalCollected").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.TotalCommission).HasColumnName("totalCommission").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.ExpectedHandover).HasColumnName("expectedHandover").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.DeclaredAmount).HasColumnName("declaredAmount").HasPrecision(12, 2);
        builder.Property(e => e.Variance).HasColumnName("variance").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.SettlementMode).HasColumnName("settlementMode").HasConversion<string>().IsRequired();
        builder.Property(e => e.PayModel).HasColumnName("payModel").HasConversion<string>().IsRequired();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.ValidatedAt).HasColumnName("validatedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ValidatedById).HasColumnName("validatedById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => new { e.CourierId, e.Day }).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.AgencyId, e.Day });
        builder.HasOne(e => e.Courier).WithMany(e => e.CashSessions).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.CashSessions).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.ValidatedBy).WithMany(e => e.ValidatedCashSessions).HasForeignKey(e => e.ValidatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
