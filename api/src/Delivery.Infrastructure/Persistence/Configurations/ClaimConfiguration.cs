using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("claims");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.TypeId).HasColumnName("typeId").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.Subject).HasColumnName("subject").IsRequired();
        builder.Property(e => e.Message).HasColumnName("message").IsRequired();
        builder.Property(e => e.ParcelId).HasColumnName("parcelId");
        builder.Property(e => e.ParcelCode).HasColumnName("parcelCode");
        builder.Property(e => e.AssigneeId).HasColumnName("assigneeId");
        builder.Property(e => e.DueAt).HasColumnName("dueAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ResolvedAt).HasColumnName("resolvedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.AuthorName).HasColumnName("authorName").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.ClientId, e.CreatedAt });
        builder.HasIndex(e => e.AssigneeId);
        builder.HasOne(e => e.Client).WithMany(e => e.Claims).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Type).WithMany(e => e.Claims).HasForeignKey(e => e.TypeId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Parcel).WithMany(e => e.Claims).HasForeignKey(e => e.ParcelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Assignee).WithMany(e => e.AssignedClaims).HasForeignKey(e => e.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Author).WithMany(e => e.AuthoredClaims).HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}
