using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ClaimAttachmentConfiguration : IEntityTypeConfiguration<ClaimAttachment>
{
    public void Configure(EntityTypeBuilder<ClaimAttachment> builder)
    {
        builder.ToTable("claim_attachments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ClaimId).HasColumnName("claimId").IsRequired();
        builder.Property(e => e.EventId).HasColumnName("eventId");
        builder.Property(e => e.Filename).HasColumnName("filename").IsRequired();
        builder.Property(e => e.StorageKey).HasColumnName("storageKey").IsRequired();
        builder.Property(e => e.MimeType).HasColumnName("mimeType").IsRequired();
        builder.Property(e => e.SizeBytes).HasColumnName("sizeBytes").IsRequired();
        builder.Property(e => e.UploadedById).HasColumnName("uploadedById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => e.ClaimId);
        builder.HasOne(e => e.Claim).WithMany(e => e.Attachments).HasForeignKey(e => e.ClaimId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Event).WithMany(e => e.Attachments).HasForeignKey(e => e.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.UploadedBy).WithMany(e => e.ClaimAttachments).HasForeignKey(e => e.UploadedById).OnDelete(DeleteBehavior.SetNull);
    }
}
