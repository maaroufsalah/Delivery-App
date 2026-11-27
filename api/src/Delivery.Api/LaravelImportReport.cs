using System.Text;
using Delivery.Infrastructure.Persistence.Seed;

namespace Delivery.Api;

/// <summary>Le compte rendu de `import-laravel`, par table : lus, importés, ignorés et leurs raisons, notes.</summary>
public static class LaravelImportReport
{
    public static string Render(LaravelImporter.Report report)
    {
        var text = new StringBuilder();
        text.AppendLine(report.Applied ? "IMPORT WIMO LARAVEL — APPLIQUÉ" : "IMPORT WIMO LARAVEL — À BLANC (rien n'est écrit ; relancer avec --apply)");
        text.AppendLine();
        text.AppendLine($"{"table",-28}{"lus",8}{"importés",10}{"ignorés",9}");
        foreach (var table in report.Tables) text.AppendLine($"{table.Table,-28}{table.Read,8}{table.Imported,10}{table.IgnoredCount,9}");
        foreach (var table in report.Tables.Where(t => t.IgnoredCount > 0 || t.Notes.Count > 0))
        {
            text.AppendLine();
            text.AppendLine($"== {table.Table}");
            foreach (var (reason, ids) in table.Ignored) text.AppendLine($"  ignoré · {reason} ({ids.Count}) : {string.Join(", ", ids.Take(40))}{(ids.Count > 40 ? ", …" : string.Empty)}");
            foreach (var note in table.Notes) text.AppendLine($"  note · {note}");
        }
        return text.ToString();
    }
}
