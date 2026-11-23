using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("routes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.StatusAt).HasColumnName("statusAt").HasColumnType("timestamptz(3)").IsRequired();
        builder.Property(e => e.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
        builder.Property(e => e.CityId).HasColumnName("cityId").IsRequired();
        builder.Property(e => e.OriginCityId).HasColumnName("originCityId").IsRequired();
        builder.Property(e => e.CourierId).HasColumnName("courierId").IsRequired();
        builder.Property(e => e.ExitDate).HasColumnName("exitDate").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.CourierId);
        builder.HasIndex(e => e.CityId);
        builder.HasOne(e => e.City).WithMany(e => e.Routes).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.OriginCity).WithMany(e => e.RouteOrigins).HasForeignKey(e => e.OriginCityId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Courier).WithMany(e => e.Routes).HasForeignKey(e => e.CourierId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
