using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ParcelEventConfiguration : IEntityTypeConfiguration<ParcelEvent>
{
    public void Configure(EntityTypeBuilder<ParcelEvent> builder)
    {
        builder.ToTable("parcel_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId").IsRequired();
        builder.Property(e => e.StatusId).HasColumnName("statusId").IsRequired();
        builder.Property(e => e.Comment).HasColumnName("comment");
        builder.Property(e => e.AuthorId).HasColumnName("authorId");
        builder.Property(e => e.AuthorName).HasColumnName("authorName");
        builder.Property(e => e.CityId).HasColumnName("cityId");
        builder.Property(e => e.AttachmentName).HasColumnName("attachmentName");
        builder.Property(e => e.AttachmentPath).HasColumnName("attachmentPath");
        builder.Property(e => e.AttachmentMimeType).HasColumnName("attachmentMimeType");
        builder.Property(e => e.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
        builder.Property(e => e.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
        builder.Property(e => e.ScheduledDate).HasColumnName("scheduledDate").HasColumnType("timestamptz(3)");
        builder.Property(e => e.IsDispute).HasColumnName("isDispute").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.ParcelId, e.CreatedAt });
        builder.HasIndex(e => new { e.StatusId, e.CreatedAt });
        builder.HasOne(e => e.Parcel).WithMany(e => e.Events).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Status).WithMany(e => e.Events).HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Author).WithMany(e => e.ParcelEvents).HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.City).WithMany(e => e.ParcelEvents).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict);
    }
}
