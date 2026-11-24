using Delivery.Api;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    if (args.Length >= 2 && args[0] == "import-referential")
    {
        // `dotnet Delivery.Api.dll import-referential <dossier>` : référentiel Wimo depuis des CSV.
        builder.AddDeliveryApi();
        var tool = builder.Build();
        using var scope = tool.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Delivery.Infrastructure.Persistence.AppDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Delivery.Infrastructure.Persistence.Seed.DatabaseSeeder>().SeedAsync();
        var report = await scope.ServiceProvider.GetRequiredService<Delivery.Infrastructure.Persistence.Seed.ReferentialImporter>().ImportAsync(args[1]);
        Log.Information("Référentiel importé : {Zones} zones, {Cities} villes, {Districts} quartiers, {Agencies} agences, {Rules} tarifs, {Warnings} avertissements",
            report.Zones, report.Cities, report.Districts, report.Agencies, report.Rules, report.Warnings.Count);
        return;
    }
    builder.AddDeliveryApi();
    var app = builder.Build();
    app.UseDeliveryApi();
    await app.PrepareDatabaseAsync();
    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Arrêt de l'API sur une erreur");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
