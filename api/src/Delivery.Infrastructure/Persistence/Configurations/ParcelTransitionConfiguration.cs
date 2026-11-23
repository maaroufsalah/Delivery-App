using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ParcelTransitionConfiguration : IEntityTypeConfiguration<ParcelTransition>
{
    public void Configure(EntityTypeBuilder<ParcelTransition> builder)
    {
        builder.ToTable("parcel_transitions");
        builder.HasKey(e => new { e.FromStatusId, e.ToStatusId, e.Kind });
        builder.Property(e => e.FromStatusId).HasColumnName("fromStatusId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ToStatusId).HasColumnName("toStatusId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().IsRequired().ValueGeneratedNever();
        builder.HasIndex(e => e.FromStatusId);
        builder.HasOne(e => e.FromStatus).WithMany(e => e.TransitionsFrom).HasForeignKey(e => e.FromStatusId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.ToStatus).WithMany(e => e.TransitionsTo).HasForeignKey(e => e.ToStatusId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
