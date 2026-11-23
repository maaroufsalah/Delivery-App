using Delivery.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Delivery.Infrastructure.Persistence;

/// <summary>Reproduit `@default(now())` et `@updatedAt` de Prisma avec <see cref="IClock"/>.</summary>
public sealed class TimestampsInterceptor(IClock clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;
        var now = clock.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            foreach (var property in entry.Properties)
            {
                var meta = property.Metadata;
                if (meta.FindAnnotation(PersistenceAnnotations.UpdatedAt) is not null)
                {
                    // Prisma n'écrase pas une valeur explicitement fournie à la création.
                    if (entry.State == EntityState.Modified || property.CurrentValue is DateTime { Ticks: 0 })
                    {
                        property.CurrentValue = now;
                    }
                }
                else if (entry.State == EntityState.Added
                         && meta.FindAnnotation(PersistenceAnnotations.DefaultNow) is not null
                         && property.CurrentValue is DateTime { Ticks: 0 })
                {
                    property.CurrentValue = now;
                }
            }
        }
    }
}
