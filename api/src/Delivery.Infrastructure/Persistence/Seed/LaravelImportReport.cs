using System.Text;

namespace Delivery.Infrastructure.Persistence.Seed;

/// <summary>Le compte rendu de la reprise Wimo Laravel (Markdown), par table : lus, créés, mis à jour, identiques, rejetés et raisons, notes.</summary>
public static class LaravelImportReport
{
    public static string Render(LaravelImporter.Report report, DateTime at)
    {
        var text = new StringBuilder();
        text.AppendLine(report.Applied ? "# Reprise Wimo Laravel — APPLIQUÉE" : "# Reprise Wimo Laravel — ESSAI À BLANC (transaction annulée, rien n'est écrit)");
        text.AppendLine();
        text.AppendLine($"Le {at:yyyy-MM-dd HH:mm} UTC.");
        text.AppendLine();
        text.AppendLine("| Table Laravel | Lues | Créées | Mises à jour | Identiques | Rejetées |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var table in report.Tables)
        {
            text.AppendLine($"| {table.Table} | {table.Read} | {table.Imported} | {table.Updated} | {table.Unchanged} | {table.IgnoredCount} |");
        }
        foreach (var table in report.Tables.Where(t => t.IgnoredCount > 0 || t.Notes.Count > 0))
        {
            text.AppendLine();
            text.AppendLine($"## {table.Table}");
            text.AppendLine();
            foreach (var (reason, ids) in table.Ignored) text.AppendLine($"- rejeté · {reason} ({ids.Count}) : {string.Join(", ", ids.Take(60))}{(ids.Count > 60 ? ", …" : string.Empty)}");
            foreach (var note in table.Notes) text.AppendLine($"- note · {note}");
        }
        return text.ToString();
    }
}
