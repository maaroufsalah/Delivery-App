using System.Globalization;
using System.Text;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using FluentValidation;
using MediatR;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard/export` : une section (livreurs, villes, clients) en Excel ou CSV, filtres et tri compris, 5 000 lignes.</summary>
public sealed record ExportDashboardQuery(DashboardFilters Filters, string? Section, string? Format, string? Sort) : IRequest<FileContent>, IDashboardRequest;

public sealed class ExportDashboardValidator : DashboardValidator<ExportDashboardQuery>
{
    public ExportDashboardValidator()
    {
        RuleFor(x => x.Section).Must(v => v is not null && DashboardOptions.ExportSections.Contains(v)).WithMessage(Zod.InvalidOption(DashboardOptions.ExportSections));
        OneOfRule(x => x.Format, "format", DashboardOptions.ExportFormats);
    }
}

internal sealed class ExportDashboardHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IExcelService excel) : IRequestHandler<ExportDashboardQuery, FileContent>
{
    private const int ExportPage = 5_000;

    private static string Pct(double? value) =>
        value is null ? "—" : (Math.Floor(value.Value * 1000 + 0.5) / 10).ToString("F1", CultureInfo.InvariantCulture) + " %";

    private static double Money(long cents) => cents / 100d;

    private static readonly IReadOnlyList<ExportColumn<DashboardCourierRowDto>> CourierColumns =
    [
        new("Rang", r => r.Rank),
        new("Livreur", r => r.CourierName),
        new("Colis attribués", r => r.Assigned),
        new("Livrés", r => r.Delivered),
        new("Retours", r => r.Returned),
        new("Taux de réussite", r => Pct(r.SuccessRate)),
        new("Délai moyen (h)", r => r.AvgDelayHours is { } hours ? Math.Floor(hours * 10 + 0.5) / 10 : (object)"—"),
        new("COD encaissé", r => Money(r.CodCollected)),
        new("Livrés période précédente", r => r.PreviousDelivered is { } previous ? previous : (object)"—"),
    ];

    private static readonly IReadOnlyList<ExportColumn<DashboardCityRowDto>> CityColumns =
    [
        new("Ville", r => r.CityName),
        new("Colis", r => r.Total),
        new("Livrés", r => r.Delivered),
        new("Retours", r => r.Returned),
        new("Taux de réussite", r => Pct(r.SuccessRate)),
        new("Taux de retour", r => Pct(r.ReturnRate)),
        new("COD encaissé", r => Money(r.CodCollected)),
    ];

    private static readonly IReadOnlyList<ExportColumn<DashboardClientRowDto>> ClientColumns =
    [
        new("Rang", r => r.Rank),
        new("Client", r => r.ClientName),
        new("Pro", r => r.IsPro ? "Oui" : "Non"),
        new("Colis", r => r.Total),
        new("Livrés", r => r.Delivered),
        new("Retours", r => r.Returned),
        new("Taux de réussite", r => Pct(r.SuccessRate)),
        new("COD encaissé", r => Money(r.CodCollected)),
    ];

    public async Task<FileContent> Handle(ExportDashboardQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        var sort = request.Sort?.Trim();
        var csv = request.Format == "csv";
        switch (request.Section)
        {
            case "couriers":
            {
                var page = await DashboardRankings.CouriersAsync(db, ctx, 1, ExportPage, OneOf(sort, DashboardOptions.CourierSorts, "delivered"), cancellationToken);
                return await RenderAsync("Livreurs", CourierColumns, page.Items, csv, cancellationToken);
            }
            case "cities":
            {
                var page = await DashboardRankings.CitiesAsync(db, ctx, 1, ExportPage, OneOf(sort, DashboardOptions.CitySorts, "total"), cancellationToken);
                return await RenderAsync("Villes", CityColumns, page.Items, csv, cancellationToken);
            }
            default:
            {
                var page = await DashboardRankings.ClientsAsync(db, ctx, 1, ExportPage, OneOf(sort, DashboardOptions.ClientRanks, "volume"), cancellationToken);
                return await RenderAsync("Clients", ClientColumns, page.Items, csv, cancellationToken);
            }
        }
    }

    private static string OneOf(string? value, IReadOnlyCollection<string> allowed, string fallback) => value is not null && allowed.Contains(value) ? value : fallback;

    private Task<FileContent> RenderAsync<T>(string sheet, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows, bool csv, CancellationToken cancellationToken) =>
        csv ? Task.FromResult(Csv(sheet, columns, rows)) : Exports.ToXlsxAsync(db, excel, clock, caller, sheet, columns, rows, cancellationToken);

    /// <summary>`csvResponse` : point-virgule, virgule décimale, BOM UTF-8, fins de ligne CRLF.</summary>
    private FileContent Csv<T>(string sheet, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows)
    {
        var lines = new List<string> { string.Join(';', columns.Select(c => Cell(c.Header))) };
        lines.AddRange(rows.Select(row => string.Join(';', columns.Select(c => Cell(c.Value(row))))));
        var content = Encoding.UTF8.GetBytes("﻿" + string.Join("\r\n", lines) + "\r\n");
        // `new Date().toISOString().slice(0, 10)` : la date UTC du serveur.
        var stamp = clock.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new FileContent(content, "text/csv; charset=utf-8", $"tableau-de-bord-{sheet.ToLowerInvariant()}-{stamp}.csv", CacheControl: "private, no-store");
    }

    /// <summary>`cell` : un nombre prend la virgule décimale ; guillemets si `"`, `;` ou fin de ligne.</summary>
    private static string Cell(object? value)
    {
        var text = value switch
        {
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            double d => JsNumber(d).Replace('.', ','),
            _ => value?.ToString() ?? string.Empty,
        };
        return text.IndexOfAny(['"', ';', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : text;
    }

    /// <summary>`String(nombre)` de JS pour les valeurs d'un export (entiers et décimaux courts).</summary>
    private static string JsNumber(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
