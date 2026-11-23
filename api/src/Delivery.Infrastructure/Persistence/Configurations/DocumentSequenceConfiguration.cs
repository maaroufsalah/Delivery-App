using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("document_sequences");
        builder.HasKey(e => new { e.Kind, e.Period });
        builder.Property(e => e.Kind).HasColumnName("kind").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Period).HasColumnName("period").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.LastRank).HasColumnName("lastRank").IsRequired();
    }
}
