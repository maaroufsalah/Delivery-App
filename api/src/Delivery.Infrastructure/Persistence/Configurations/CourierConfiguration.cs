using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CourierConfiguration : IEntityTypeConfiguration<Courier>
{
    public void Configure(EntityTypeBuilder<Courier> builder)
    {
        builder.ToTable("couriers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("userId");
        builder.HasIndex(e => e.UserId).IsUnique();
        builder.Property(e => e.FirstName).HasColumnName("firstName").IsRequired();
        builder.Property(e => e.LastName).HasColumnName("lastName").IsRequired();
        builder.Property(e => e.Phone).HasColumnName("phone").IsRequired();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId");
        builder.Property(e => e.NationalId).HasColumnName("nationalId");
        builder.Property(e => e.VehicleType).HasColumnName("vehicleType").HasConversion<string>().IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("isActive").IsRequired();
        builder.Property(e => e.BankName).HasColumnName("bankName");
        builder.Property(e => e.Rib).HasColumnName("rib");
        builder.Property(e => e.PayModel).HasColumnName("payModel").HasConversion<string>().IsRequired();
        builder.Property(e => e.SettlementMode).HasColumnName("settlementMode").HasConversion<string>().IsRequired();
        builder.Property(e => e.PayPeriod).HasColumnName("payPeriod").HasConversion<string>().IsRequired();
        builder.Property(e => e.FixedSalary).HasColumnName("fixedSalary").HasPrecision(12, 2);
        builder.Property(e => e.ValidatedAt).HasColumnName("validatedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.ValidatedById).HasColumnName("validatedById");
        builder.Property(e => e.RejectedAt).HasColumnName("rejectedAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.RejectedById).HasColumnName("rejectedById");
        builder.Property(e => e.RejectionReason).HasColumnName("rejectionReason");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasOne(e => e.User).WithOne(e => e.Courier).HasForeignKey<Courier>(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.City).WithMany(e => e.Couriers).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.Couriers).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ValidatedBy).WithMany(e => e.ValidatedCouriers).HasForeignKey(e => e.ValidatedById).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.RejectedBy).WithMany(e => e.RejectedCouriers).HasForeignKey(e => e.RejectedById).OnDelete(DeleteBehavior.SetNull);
    }
}
