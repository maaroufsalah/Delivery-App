using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ParcelCodeSequenceConfiguration : IEntityTypeConfiguration<ParcelCodeSequence>
{
    public void Configure(EntityTypeBuilder<ParcelCodeSequence> builder)
    {
        builder.ToTable("parcel_code_sequences");
        builder.HasKey(e => new { e.Day, e.OriginCityId, e.DestinationCityId });
        builder.Property(e => e.Day).HasColumnName("day").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.OriginCityId).HasColumnName("originCityId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.DestinationCityId).HasColumnName("destinationCityId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.LastRank).HasColumnName("lastRank").IsRequired();
    }
}
