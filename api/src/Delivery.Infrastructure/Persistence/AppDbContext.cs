using Delivery.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Infrastructure.Persistence;

public static class PersistenceAnnotations
{
    /// <summary>`@default(now())` de Prisma : posé par l'horloge à l'insertion.</summary>
    public const string DefaultNow = "Delivery:DefaultNow";

    /// <summary>`@updatedAt` de Prisma : posé par l'horloge à chaque écriture.</summary>
    public const string UpdatedAt = "Delivery:UpdatedAt";
}

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
