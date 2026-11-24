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

/// <summary>`GET /parcels/imports/:id/report.xlsx` : les anomalies journalisées d'un import.</summary>
public sealed record GetParcelImportReportQuery(string Id) : IRequest<FileContent>;

internal sealed class GetParcelImportReportHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel) : IRequestHandler<GetParcelImportReportQuery, FileContent>
{
    public async Task<FileContent> Handle(GetParcelImportReportQuery request, CancellationToken cancellationToken)
    {
        var where = db.ParcelImports.AsNoTracking().Where(i => i.Id == request.Id);
        if (caller.ClientId is { } own) where = where.Where(i => i.ClientId == own);
        var json = await where.Select(i => i.Errors).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound("Import introuvable.");
        var errors = new List<IReadOnlyList<object?>>();
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in document.RootElement.EnumerateArray())
                {
                    string Text(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : string.Empty;
                    object? row = e.TryGetProperty("row", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : null;
                    errors.Add([row, Text("column"), Text("code"), Text("message")]);
                }
            }
        }
        var content = excel.Write("Erreurs", ["Ligne", "Colonne", "Code", "Message"], errors, null);
        return new FileContent(content, FileContent.Xlsx, $"rapport-import-{request.Id}.xlsx") { CacheControl = "private, no-store" };
    }
}
