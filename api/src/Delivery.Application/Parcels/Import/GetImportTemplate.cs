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

/// <summary>`GET /parcels/import/template.xlsx` : le modèle, listes déroulantes et aide dans la langue de la société.</summary>
public sealed record GetImportTemplateQuery : IRequest<FileContent>;

internal sealed class GetImportTemplateHandler(IAppDbContext db, IExcelService excel) : IRequestHandler<GetImportTemplateQuery, FileContent>
{
    public async Task<FileContent> Handle(GetImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.DefaultLocale, c.Name }).FirstAsync(cancellationToken);
        var cityNames = await db.Cities.AsNoTracking().Where(c => c.DeletedAt == null && c.IsActive).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync(cancellationToken);
        var districtNames = (await db.Districts.AsNoTracking().Where(d => d.DeletedAt == null).OrderBy(d => d.Name).Select(d => d.Name).ToListAsync(cancellationToken)).Distinct().ToList();
        var locale = ImportColumns.Locale(company.DefaultLocale);
        var labels = ImportColumns.Headers[locale];
        var help = ImportColumns.Help(locale);
        var yes = locale switch { "en" => "Y", "ar" => "ن", _ => "O" };
        var no = locale == "ar" ? "ل" : "N";
        var examples = new Dictionary<string, string>
        {
            ["externalCode"] = "CMD-1042",
            ["recipientName"] = "Youssef El Amrani",
            ["recipientPhone"] = "0612345678",
            ["city"] = cityNames.FirstOrDefault() ?? "Casablanca",
            ["district"] = districtNames.FirstOrDefault() ?? "Maârif",
            ["address"] = "12 rue des Orangers, résidence Al Amal, appartement 4",
            ["cod"] = "350",
            ["description"] = "Chaussures, pointure 42",
            ["openOnDelivery"] = no,
            ["fragile"] = no,
            ["notes"] = string.Empty,
            ["quantity"] = string.Empty,
        };
        var choices = new Dictionary<string, IReadOnlyList<string>>
        {
            ["city"] = cityNames,
            ["district"] = districtNames,
            ["openOnDelivery"] = [yes, no],
            ["fragile"] = [yes, no],
        };
        var columns = ImportColumns.Fields.Select(field => new ExcelTemplateColumn(labels[field], examples[field], choices.GetValueOrDefault(field),
            ImportColumns.Required.Contains(field), field == "district", field is "address" or "description" ? 34 : null, field == "recipientPhone")).ToList();
        var content = excel.WriteTemplate(new ExcelTemplate(help.Sheet, "_listes", 500, columns, help.Name, $"{company.Name}, {help.Title}", help.Rows));
        return new FileContent(content, FileContent.Xlsx, "modele-import-colis.xlsx") { CacheControl = "private, no-store" };
    }
}
