using Delivery.Application.Common.Interfaces;
using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default manquant.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<TimestampsInterceptor>();
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations"));
            options.AddInterceptors(provider.GetRequiredService<TimestampsInterceptor>());
        });
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        return services;
    }
}
