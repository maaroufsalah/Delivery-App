using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ProviderCallConfiguration : IEntityTypeConfiguration<ProviderCall>
{
    public void Configure(EntityTypeBuilder<ProviderCall> builder)
    {
        builder.ToTable("provider_calls");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.TokenId).HasColumnName("tokenId").IsRequired();
        builder.Property(e => e.EventKey).HasColumnName("eventKey").HasMaxLength(80);
        builder.Property(e => e.ParcelCode).HasColumnName("parcelCode").HasMaxLength(80).IsRequired();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId");
        builder.Property(e => e.ProviderStatus).HasColumnName("providerStatus").IsRequired();
        builder.Property(e => e.MappedStatus).HasColumnName("mappedStatus");
        builder.Property(e => e.OccurredAt).HasColumnName("occurredAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.Mode).HasColumnName("mode").HasConversion<string>().IsRequired();
        builder.Property(e => e.Outcome).HasColumnName("outcome").HasConversion<string>().IsRequired();
        builder.Property(e => e.HttpStatus).HasColumnName("httpStatus").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(80);
        builder.Property(e => e.RequestId).HasColumnName("requestId").HasMaxLength(80);
        builder.Property(e => e.Alert).HasColumnName("alert").IsRequired();
        builder.Property(e => e.AlertResolvedAt).HasColumnName("alertResolvedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.At).HasColumnName("at").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => e.At);
        builder.HasIndex(e => new { e.TokenId, e.At });
        builder.HasIndex(e => new { e.TokenId, e.ParcelId, e.OccurredAt });
        builder.HasIndex(e => new { e.TokenId, e.Alert, e.AlertResolvedAt });
        builder.HasOne(e => e.Token).WithMany(e => e.Calls).HasForeignKey(e => e.TokenId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
