using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(e => new { e.UserId, e.Topic });
        builder.Property(e => e.UserId).HasColumnName("userId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Topic).HasColumnName("topic").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.UserId);
        builder.HasOne(e => e.User).WithMany(e => e.NotificationPreferences).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
