using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId");
        builder.Property(e => e.UserId).HasColumnName("userId");
        builder.HasIndex(e => e.UserId).IsUnique();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.LegalName).HasColumnName("legalName");
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<string>().IsRequired();
        builder.Property(e => e.TaxId).HasColumnName("taxId");
        builder.Property(e => e.Phone).HasColumnName("phone").IsRequired();
        builder.Property(e => e.Email).HasColumnName("email").IsRequired();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired();
        builder.Property(e => e.Address).HasColumnName("address");
        builder.Property(e => e.Notes).HasColumnName("notes");
        builder.Property(e => e.IsActive).HasColumnName("isActive").IsRequired();
        builder.Property(e => e.IsPro).HasColumnName("isPro").IsRequired();
        builder.Property(e => e.BillingType).HasColumnName("billingType").HasConversion<string>().IsRequired();
        builder.Property(e => e.MonthlyBilling).HasColumnName("monthlyBilling").IsRequired();
        builder.Property(e => e.VatRate).HasColumnName("vatRate");
        builder.Property(e => e.PickupFee).HasColumnName("pickupFee").HasPrecision(12, 2);
        builder.Property(e => e.FreePickupThreshold).HasColumnName("freePickupThreshold");
        builder.Property(e => e.ValidatedAt).HasColumnName("validatedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ValidatedById).HasColumnName("validatedById");
        builder.Property(e => e.RejectedAt).HasColumnName("rejectedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.RejectedById).HasColumnName("rejectedById");
        builder.Property(e => e.RejectionReason).HasColumnName("rejectionReason");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => e.AgencyId);
        builder.HasOne(e => e.Agency).WithMany(e => e.Clients).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.User).WithOne(e => e.Client).HasForeignKey<Client>(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.City).WithMany(e => e.Clients).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.ValidatedBy).WithMany(e => e.ValidatedClients).HasForeignKey(e => e.ValidatedById).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.RejectedBy).WithMany(e => e.RejectedClients).HasForeignKey(e => e.RejectedById).OnDelete(DeleteBehavior.SetNull);
    }
}
