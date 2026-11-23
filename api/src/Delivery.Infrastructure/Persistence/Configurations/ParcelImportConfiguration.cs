using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ParcelImportConfiguration : IEntityTypeConfiguration<ParcelImport>
{
    public void Configure(EntityTypeBuilder<ParcelImport> builder)
    {
        builder.ToTable("parcel_imports");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.Filename).HasColumnName("filename").IsRequired();
        builder.Property(e => e.StorageKey).HasColumnName("storageKey");
        builder.Property(e => e.Mode).HasColumnName("mode").HasConversion<string>().IsRequired();
        builder.Property(e => e.RowCount).HasColumnName("rowCount").IsRequired();
        builder.Property(e => e.OkCount).HasColumnName("okCount").IsRequired();
        builder.Property(e => e.KoCount).HasColumnName("koCount").IsRequired();
        builder.Property(e => e.Errors).HasColumnName("errors").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.AuthorName).HasColumnName("authorName").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.ClientId);
        builder.HasOne(e => e.Client).WithMany(e => e.ParcelImports).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.CreatedBy).WithMany(e => e.ParcelImports).HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
