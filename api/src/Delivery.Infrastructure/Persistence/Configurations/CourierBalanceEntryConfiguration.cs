using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CourierBalanceEntryConfiguration : IEntityTypeConfiguration<CourierBalanceEntry>
{
    public void Configure(EntityTypeBuilder<CourierBalanceEntry> builder)
    {
        builder.ToTable("courier_balance_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.SessionId).HasColumnName("sessionId");
        builder.Property(e => e.RemittanceId).HasColumnName("remittanceId");
        builder.Property(e => e.Method).HasColumnName("method").HasConversion<string>();
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.AuthorName).HasColumnName("authorName").IsRequired();
        builder.Property(e => e.At).HasColumnName("at").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.CourierId, e.At });
        builder.HasIndex(e => e.SessionId);
        builder.HasIndex(e => e.RemittanceId);
        builder.HasOne(e => e.Courier).WithMany(e => e.BalanceEntries).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Session).WithMany(e => e.Entries).HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Remittance).WithMany(e => e.Entries).HasForeignKey(e => e.RemittanceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Author).WithMany(e => e.BalanceEntries).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
