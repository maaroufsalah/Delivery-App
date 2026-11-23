using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CourierStatementLineConfiguration : IEntityTypeConfiguration<CourierStatementLine>
{
    public void Configure(EntityTypeBuilder<CourierStatementLine> builder)
    {
        builder.ToTable("courier_statement_lines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.StatementId).HasColumnName("statementId").IsRequired();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
        builder.Property(e => e.ParcelCode).HasColumnName("parcelCode").IsRequired();
        builder.Property(e => e.CityName).HasColumnName("cityName").IsRequired();
        builder.Property(e => e.StatusId).HasColumnName("statusId").IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Deducted).HasColumnName("deducted").IsRequired();
        builder.Property(e => e.WorkedAt).HasColumnName("workedAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.SettledParcelId).HasColumnName("settledParcelId");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.StatementId, e.ParcelId, e.Kind }).IsUnique();
        builder.HasIndex(e => new { e.SettledParcelId, e.Kind }).IsUnique();
        builder.HasIndex(e => e.ParcelId);
        builder.HasOne(e => e.Statement).WithMany(e => e.Lines).HasForeignKey(e => e.StatementId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.StatementLines).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
