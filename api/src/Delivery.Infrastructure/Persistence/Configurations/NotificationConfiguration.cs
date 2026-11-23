using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("userId").IsRequired();
        builder.Property(e => e.Topic).HasColumnName("topic").IsRequired();
        builder.Property(e => e.Params).HasColumnName("params").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.ReadAt).HasColumnName("readAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => new { e.UserId, e.CreatedAt });
        builder.HasIndex(e => new { e.UserId, e.ReadAt });
        builder.HasOne(e => e.User).WithMany(e => e.Notifications).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
