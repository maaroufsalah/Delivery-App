using Delivery.Application.Common.Interfaces;
using Delivery.Infrastructure.Documents;
using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Persistence.Seed;
using Delivery.Infrastructure.Security;
using Delivery.Infrastructure.Storage;
using Delivery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<TimestampsInterceptor>();
        services.AddScoped<AuditInterceptor>();
        services.AddSingleton<PushQueue>();
        services.AddScoped<PushInterceptor>();
        services.AddHttpClient<ExpoPushClient>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IExcelService, ExcelService>();
        services.AddSingleton<IPdfService, PdfService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<ReferentialImporter>();
        services.AddScoped<LaravelImporter>();
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            // Lue à la résolution, pas à l'enregistrement : la configuration finale fait foi.
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default manquant.");
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations"));
            options.AddInterceptors(provider.GetRequiredService<TimestampsInterceptor>(), provider.GetRequiredService<AuditInterceptor>(), provider.GetRequiredService<PushInterceptor>());
        });
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        return services;
    }
}
