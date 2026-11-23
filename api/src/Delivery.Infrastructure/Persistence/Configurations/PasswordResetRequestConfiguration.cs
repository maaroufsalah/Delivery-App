using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetRequestConfiguration : IEntityTypeConfiguration<PasswordResetRequest>
{
    public void Configure(EntityTypeBuilder<PasswordResetRequest> builder)
    {
        builder.ToTable("password_reset_requests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Email).HasColumnName("email").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("userId");
        builder.Property(e => e.Ip).HasColumnName("ip");
        builder.Property(e => e.UserAgent).HasColumnName("userAgent");
        builder.Property(e => e.MailProvider).HasColumnName("mailProvider").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.Email);
        builder.HasOne(e => e.User).WithMany(e => e.PasswordResetRequests).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
