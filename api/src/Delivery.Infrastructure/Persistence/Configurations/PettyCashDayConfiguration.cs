using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class PettyCashDayConfiguration : IEntityTypeConfiguration<PettyCashDay>
{
    public void Configure(EntityTypeBuilder<PettyCashDay> builder)
    {
        builder.ToTable("petty_cash_days");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Day).HasColumnName("day").IsRequired();
        builder.Property(e => e.OpeningBalance).HasColumnName("openingBalance").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Supplied).HasColumnName("supplied").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Spent).HasColumnName("spent").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.ClosingBalance).HasColumnName("closingBalance").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.PendingDrafts).HasColumnName("pendingDrafts").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.ClosedAt).HasColumnName("closedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ClosedById).HasColumnName("closedById");
        builder.Property(e => e.ClosedAutomatically).HasColumnName("closedAutomatically").IsRequired();
        builder.Property(e => e.ReopenedAt).HasColumnName("reopenedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ReopenedById).HasColumnName("reopenedById");
        builder.Property(e => e.ReopenReason).HasColumnName("reopenReason");
        builder.Property(e => e.ReopenCount).HasColumnName("reopenCount").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => new { e.AgencyId, e.Day }).IsUnique();
        builder.HasIndex(e => e.Day);
        builder.HasIndex(e => new { e.Status, e.Day });
        builder.HasOne(e => e.Agency).WithMany(e => e.PettyCashDays).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.ClosedBy).WithMany(e => e.PettyCashDaysClosed).HasForeignKey(e => e.ClosedById).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.ReopenedBy).WithMany(e => e.PettyCashDaysReopened).HasForeignKey(e => e.ReopenedById).OnDelete(DeleteBehavior.SetNull);
    }
}
