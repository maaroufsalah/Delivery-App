using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Import;

/// <summary>`POST /parcels/import/anomalies.xlsx` : les erreurs puis les avertissements, ligne par ligne.</summary>
public sealed record ExportImportAnomaliesCommand : ImportSource, IRequest<FileContent>;

public sealed class ExportImportAnomaliesValidator : ImportSourceValidator<ExportImportAnomaliesCommand>;

internal sealed class ExportImportAnomaliesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel) : IRequestHandler<ExportImportAnomaliesCommand, FileContent>
{
    public async Task<FileContent> Handle(ExportImportAnomaliesCommand input, CancellationToken cancellationToken)
    {
        var context = await ParcelImports.ContextAsync(db, caller, input.ClientId, cancellationToken);
        var (items, _) = await ParcelImports.ValidateAsync(db, context, ParcelImports.ReadRows(excel, input.Content, input.Rows), cancellationToken);
        var locale = ImportColumns.Locale(await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.DefaultLocale).FirstAsync(cancellationToken));
        var labels = ImportColumns.Headers[locale];
        var sheet = ImportColumns.AnomalySheet(locale);
        var lines = items.SelectMany(item => item.Errors.Select(a => (item, anomaly: a, level: 0)).Concat((item.Warnings ?? []).Select(a => (item, anomaly: a, level: 1))))
            .OrderBy(l => l.level).ThenBy(l => l.item.Row).ToList();
        var rows = lines.Select(l => (IReadOnlyList<object?>)
        [
            l.item.Row,
            l.item.Values.GetValueOrDefault("externalCode", string.Empty),
            l.anomaly.Column is null ? string.Empty : labels.GetValueOrDefault(l.anomaly.Column, l.anomaly.Column),
            l.level == 0 ? sheet.Error : sheet.Warning,
            l.anomaly.Message,
        ]).ToList();
        var content = excel.Write(sheet.Name, sheet.Headers, rows, null);
        return new FileContent(content, FileContent.Xlsx, "anomalies-import.xlsx") { CacheControl = "private, no-store" };
    }
}
