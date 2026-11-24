using Delivery.Api.Auth;
using Delivery.Api.Http;
using Delivery.Application.Common.Interfaces;
using Delivery.Application;
using Delivery.Domain.Common;
using Delivery.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;

namespace Delivery.Api;

public static class ApiSetup
{
    /// <summary>`BODY_LIMIT` de Speedy : 2 Mo.</summary>
    public const long BodyLimitBytes = 2 * 1024 * 1024;

    public static WebApplicationBuilder AddDeliveryApi(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, logger) => logger
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(new CompactJsonFormatter()));

        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = BodyLimitBytes);

        builder.Services.AddApplication(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<PublicRateLimiter>();
        builder.Services.AddScoped<AccessFilter>();
        builder.Services.AddHostedService<Background.LabelJobWorker>();
        builder.Services.AddHostedService<Background.BulkJobWorker>();

        builder.Services.ConfigureHttpJsonOptions(options => JsonDefaults.Apply(options.SerializerOptions));
        builder.Services
            .AddControllers(options =>
            {
                options.Conventions.Add(new ApiRoutePrefixConvention("api"));
                options.Filters.AddService<AccessFilter>();
                // Un corps absent vaut `undefined` pour zod : le validator en décide.
                options.AllowEmptyInputInBodyModelBinding = true;
            })
            .AddJsonOptions(options => JsonDefaults.Apply(options.JsonSerializerOptions))
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = InvalidModelState);

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // `trust proxy: loopback, linklocal, uniquelocal` : seuls les sauts privés sont crus.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in new[] { "127.0.0.0/8", "::1/128", "169.254.0.0/16", "fe80::/10", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "fc00::/7" })
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
            options.ForwardLimit = null;
        });

        var origins = (builder.Configuration["WEB_ORIGIN"] ?? "http://localhost:3000")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.ToLowerInvariant())
            .ToArray();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("x-request-id", "content-disposition")));

        return builder;
    }

    public static WebApplication UseDeliveryApi(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseMiddleware<RequestIdMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
                : context.Response.StatusCode >= 400 ? Serilog.Events.LogEventLevel.Warning
                : Serilog.Events.LogEventLevel.Information;
        });
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseCors();
        app.MapControllers();
        return app;
    }

    /// <summary>Migrations puis seed idempotent, si `MIGRATE_ON_STARTUP` vaut `true`.</summary>
    public static async Task PrepareDatabaseAsync(this WebApplication app)
    {
        if (!string.Equals(app.Configuration["MIGRATE_ON_STARTUP"], "true", StringComparison.OrdinalIgnoreCase)) return;
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Delivery.Infrastructure.Persistence.AppDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Delivery.Infrastructure.Persistence.Seed.DatabaseSeeder>().SeedAsync();
    }

    /// <summary>
    /// Erreur de liaison : le 422 du `ZodValidationPipe`. Un corps JSON illisible
    /// remonte en revanche en 500 chez Speedy (l'erreur de body-parser n'est pas
    /// une `HttpException`) : reproduit tel quel.
    /// </summary>
    private static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(e => e.Value is { Errors.Count: > 0 })
            .SelectMany(e => e.Value!.Errors.Select(err => new { key = e.Key, err }))
            .ToList();
        static bool IsTypeMismatch(string message) => message.Contains("could not be converted", StringComparison.Ordinal);
        if (errors.Any(e => (e.key == "$" || e.key.StartsWith("$.", StringComparison.Ordinal) || e.err.Exception is System.Text.Json.JsonException)
                            && !IsTypeMismatch(e.err.ErrorMessage + e.err.Exception?.Message)))
        {
            throw new InvalidOperationException("Corps JSON illisible.");
        }
        // Type inattendu (nombre au lieu d'une chaîne…) : zod répond « invalid_type » en 422.
        var issues = errors.Select(e => new
        {
            path = Delivery.Application.Common.Behaviors.ValidationPaths.ToPath(e.key.StartsWith("$.", StringComparison.Ordinal) ? e.key[2..] : e.key),
            message = "Invalid input",
        }).ToList();
        throw BusinessException.Validation("Saisie invalide.", new { issues });
    }
}
