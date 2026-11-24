using Delivery.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        // Clé Community lue dans l'environnement, jamais dans le dépôt.
        var licenseKey = configuration["LUCKYPENNY_LICENSE_KEY"];

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = licenseKey;
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });
        services.AddAutoMapper(cfg => cfg.LicenseKey = licenseKey, assembly);
        services.AddSingleton<Identity.LoginThrottle>();
        services.AddSingleton<Parcels.Labels.LabelJobQueue>();
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        ValidatorOptions.Global.DefaultClassLevelCascadeMode = CascadeMode.Continue;
        return services;
    }
}
