using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Title).HasColumnName("title").IsRequired();
        builder.Property(e => e.Intro).HasColumnName("intro");
        builder.Property(e => e.Body).HasColumnName("body").IsRequired();
        builder.Property(e => e.Audience).HasColumnName("audience").HasConversion<string>().IsRequired();
        builder.Property(e => e.ImageKey).HasColumnName("imageKey");
        builder.Property(e => e.ImageName).HasColumnName("imageName");
        builder.Property(e => e.IsPublished).HasColumnName("isPublished").IsRequired();
        builder.Property(e => e.PublishedAt).HasColumnName("publishedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.AuthorName).HasColumnName("authorName").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => new { e.IsPublished, e.PublishedAt });
        builder.HasIndex(e => e.Audience);
        builder.HasOne(e => e.Author).WithMany(e => e.Announcements).HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
