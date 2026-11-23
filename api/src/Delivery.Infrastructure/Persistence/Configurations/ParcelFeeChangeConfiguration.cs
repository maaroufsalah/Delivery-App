using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ParcelFeeChangeConfiguration : IEntityTypeConfiguration<ParcelFeeChange>
{
    public void Configure(EntityTypeBuilder<ParcelFeeChange> builder)
    {
        builder.ToTable("parcel_fee_changes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired();
        builder.Property(e => e.Field).HasColumnName("field").HasConversion<string>().IsRequired();
        builder.Property(e => e.OldAmount).HasColumnName("oldAmount").HasPrecision(12, 2);
        builder.Property(e => e.NewAmount).HasColumnName("newAmount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").IsRequired();
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.AuthorName).HasColumnName("authorName").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.ParcelId, e.CreatedAt });
        builder.HasIndex(e => e.CreatedAt);
        builder.HasOne(e => e.Parcel).WithMany(e => e.FeeChanges).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Author).WithMany(e => e.ParcelFeeChanges).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
