using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class RouteNoteConfiguration : IEntityTypeConfiguration<RouteNote>
{
    public void Configure(EntityTypeBuilder<RouteNote> builder)
    {
        builder.ToTable("route_notes");
        builder.HasKey(e => new { e.RouteId, e.DeliveryNoteId });
        builder.Property(e => e.RouteId).HasColumnName("routeId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.DeliveryNoteId).HasColumnName("deliveryNoteId").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Sequence).HasColumnName("sequence").IsRequired();
        builder.HasIndex(e => e.DeliveryNoteId);
        builder.HasOne(e => e.Route).WithMany(e => e.Notes).HasForeignKey(e => e.RouteId).OnDelete(DeleteBehavior.Cascade).IsRequired();
        builder.HasOne(e => e.DeliveryNote).WithMany(e => e.Routes).HasForeignKey(e => e.DeliveryNoteId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
