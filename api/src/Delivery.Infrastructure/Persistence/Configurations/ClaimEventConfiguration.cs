using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ClaimEventConfiguration : IEntityTypeConfiguration<ClaimEvent>
{
    public void Configure(EntityTypeBuilder<ClaimEvent> builder)
    {
        builder.ToTable("claim_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ClaimId).HasColumnName("claimId").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>();
        builder.Property(e => e.Body).HasColumnName("body");
        builder.Property(e => e.IsInternal).HasColumnName("isInternal").IsRequired();
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.AuthorName).HasColumnName("authorName").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.ClaimId, e.CreatedAt });
        builder.HasOne(e => e.Claim).WithMany(e => e.Events).HasForeignKey(e => e.ClaimId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Author).WithMany(e => e.ClaimEvents).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
    }
}
