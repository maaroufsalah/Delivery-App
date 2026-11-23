using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ClientDocumentConfiguration : IEntityTypeConfiguration<ClientDocument>
{
    public void Configure(EntityTypeBuilder<ClientDocument> builder)
    {
        builder.ToTable("client_documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
        builder.Property(e => e.StorageKey).HasColumnName("storageKey").IsRequired();
        builder.Property(e => e.MimeType).HasColumnName("mimeType").IsRequired();
        builder.Property(e => e.SizeBytes).HasColumnName("sizeBytes").IsRequired();
        builder.Property(e => e.UploadedById).HasColumnName("uploadedById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => e.ClientId);
        builder.HasOne(e => e.Client).WithMany(e => e.Documents).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.UploadedBy).WithMany(e => e.UploadedDocuments).HasForeignKey(e => e.UploadedById).OnDelete(DeleteBehavior.SetNull);
    }
}
