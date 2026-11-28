using System.Globalization;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Enums;
using Delivery.Infrastructure;
using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Reprise de Wimo Laravel vers Wimo .NET, relançable sans doublon (`legacy_mappings`) :
//   LegacyImport <dossier-csv> --dry-run|--apply [--batch 500] [--report rapport.md] [--migrate]
// <dossier-csv> : sortie de `scripts/laravel-dump-to-csv.py` (une table par fichier). Base : `ConnectionStrings__Default`.
// --dry-run : tout dans une transaction annulée, compte rendu réel. --apply : une transaction par lot, reprise après coupure.
// Le schéma doit être à jour (migrations appliquées au démarrage de l'API) ; `--migrate` les applique d'abord (base d'essai).

var folder = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
var apply = args.Contains("--apply");
var dryRun = args.Contains("--dry-run");
if (folder is null || apply == dryRun || !Directory.Exists(folder))
{
    Console.Error.WriteLine("Usage : LegacyImport <dossier-csv> --dry-run|--apply [--batch 500] [--report rapport.md] [--migrate]");
    return 2;
}
string? Option(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
var batch = int.TryParse(Option("--batch"), CultureInfo.InvariantCulture, out var size) && size > 0 ? size : 500;

var builder = Host.CreateApplicationBuilder([]);
builder.Configuration.AddEnvironmentVariables();
builder.Logging.ClearProviders().AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ICurrentUser, ImportCaller>();
using var host = builder.Build();
using var scope = host.Services.CreateScope();
if (args.Contains("--migrate")) await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
var importer = scope.ServiceProvider.GetRequiredService<LaravelImporter>();
var report = await importer.ImportAsync(folder, new LaravelImporter.ImportOptions(apply, batch));
// Le seed après l'import (statuts, permissions, réglages manquants) : il ne pose rien de ce que l'import a créé.
if (apply) await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
var text = LaravelImportReport.Render(report, DateTime.UtcNow);
if (Option("--report") is { } file) await File.WriteAllTextAsync(file, text);
else Console.WriteLine(text);
Console.Error.WriteLine($"{(apply ? "Appliqué" : "À blanc")} : {report.Tables.Sum(t => t.Imported)} créé(s), {report.Tables.Sum(t => t.Updated)} mis à jour, "
                        + $"{report.Tables.Sum(t => t.Unchanged)} identique(s), {report.Tables.Sum(t => t.IgnoredCount)} rejeté(s).");
return 0;

/// <summary>L'auteur des écritures de la reprise : aucun utilisateur (journal d'audit silencieux pendant l'import).</summary>
internal sealed class ImportCaller : ICurrentUser
{
    public bool IsAuthenticated => false;
    public string UserId => string.Empty;
    public string? CompanyId => null;
    public string Email => string.Empty;
    public IReadOnlyList<RoleName> Roles => [];
    public string? AgencyId => null;
    public string? ClientId => null;
    public string? CourierId => null;
    public bool MustChangePassword => false;
    public bool Impersonated => false;
    public long? IssuedAt => null;
    public string? IpAddress => null;
    public string? UserAgent => "legacy-import";
    public bool IsMobileClient => false;
    public string RequestId => "legacy-import";
    public string? PreferredLocale => null;
}
