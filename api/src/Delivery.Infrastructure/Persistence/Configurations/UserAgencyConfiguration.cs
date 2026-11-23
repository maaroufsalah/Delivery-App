using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class UserAgencyConfiguration : IEntityTypeConfiguration<UserAgency>
{
    public void Configure(EntityTypeBuilder<UserAgency> builder)
    {
        builder.ToTable("user_agencies");
        builder.HasKey(e => new { e.UserId, e.AgencyId });
        builder.Property(e => e.UserId).HasColumnName("userId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired().ValueGeneratedNever();
        builder.HasIndex(e => e.AgencyId);
        builder.HasOne(e => e.User).WithMany(e => e.Agencies).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.UserLinks).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
