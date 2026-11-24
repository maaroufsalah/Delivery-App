using System.Globalization;
using System.Text;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Warehouse.Orders;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Imports;

/// <summary>`POST /products/import` : le catalogue d'un vendeur, créé ou mis à jour par référence.</summary>
public sealed record ImportProductsCommand : WarehouseImport, IRequest<ProductImportPreviewDto>;

public sealed class ImportProductsValidator : WarehouseImportValidator<ImportProductsCommand>;

internal sealed class ImportProductsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel) : IRequestHandler<ImportProductsCommand, ProductImportPreviewDto>
{
    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["reference"] = ["reference", "ref", "sku", "code"],
        ["name"] = ["nom", "name", "libelle", "designation"],
        ["price"] = ["prix", "price", "prixunitaire"],
        ["value"] = ["valeur", "valeurdeclaree", "value"],
        ["weight"] = ["poids", "poidsg", "weight"],
        ["length"] = ["longueur", "longueurmm", "length"],
        ["width"] = ["largeur", "largeurmm", "width"],
        ["height"] = ["hauteur", "hauteurmm", "height"],
        ["stock"] = ["stock", "stockinterne"],
    };

    private sealed record Parsed(int Row, string Reference, string Name, double? Price, double? Value, int? WeightG, int? LengthMm, int? WidthMm, int? HeightMm, int? InternalStock);

    public async Task<ProductImportPreviewDto> Handle(ImportProductsCommand input, CancellationToken cancellationToken)
    {
        var clientId = await WarehouseImportRules.ClientAsync(db, caller, input.ClientId, "Choisissez l’e-commerçant du catalogue.", cancellationToken);
        var table = SheetReader.Read(excel, input.Content!);
        var columns = WarehouseImportRules.Columns(table.RawHeader, Aliases);
        if (columns["reference"] < 0 || columns["name"] < 0)
        {
            throw BusinessException.Validation("Colonnes attendues : reference, nom, puis prix, valeur, poids, longueur, largeur, hauteur, stock.", new { field = "content" });
        }
        var errors = new List<WarehouseImportError>();
        var parsed = new List<Parsed>();
        var seen = new HashSet<string>();
        for (var index = 0; index < table.Rows.Count; index++)
        {
            var cells = table.Rows[index];
            var row = index + 2;
            string Read(string key) => columns[key] >= 0 && columns[key] < cells.Count ? cells[columns[key]].Trim() : string.Empty;
            var reference = Read("reference");
            var name = Read("name");
            if (reference.Length == 0 && name.Length == 0) continue;
            if (reference.Length == 0) { errors.Add(new(row, "reference", "required", "Référence absente.")); continue; }
            if (name.Length == 0) { errors.Add(new(row, "nom", "required", "Nom absent.")); continue; }
            if (!seen.Add(reference.ToUpperInvariant())) { errors.Add(new(row, "reference", "duplicate", $"Référence « {reference} » répétée dans le fichier.")); continue; }
            var failed = false;
            double? Decimal(string key, string column)
            {
                var raw = Read(key);
                if (raw.Length == 0) return null;
                var value = WarehouseImportRules.JsNumber(raw);
                if (!double.IsFinite(value) || value < 0)
                {
                    errors.Add(new(row, column, "invalid-number", $"« {raw} » n'est pas un nombre positif."));
                    failed = true;
                    return null;
                }
                return value;
            }
            int? Integer(string key, string column) => Decimal(key, column) is { } v ? (int)Math.Floor(v + 0.5) : null;
            var price = Decimal("price", "prix");
            var declared = Decimal("value", "valeur");
            var weight = Integer("weight", "poids");
            var length = Integer("length", "longueur");
            var width = Integer("width", "largeur");
            var height = Integer("height", "hauteur");
            var stock = Integer("stock", "stock");
            if (failed) continue;
            parsed.Add(new Parsed(row, reference, name, price, declared, weight, length, width, height, stock));
        }
        var references = parsed.Select(p => p.Reference).ToList();
        var byReference = await db.Products.Where(p => p.ClientId == clientId && p.Reference != null && references.Contains(p.Reference))
            .ToDictionaryAsync(p => p.Reference!, cancellationToken);
        var created = parsed.Count(p => !byReference.ContainsKey(p.Reference));
        var updated = parsed.Count - created;
        if (input.Apply == true && errors.Count == 0)
        {
            foreach (var entry in parsed)
            {
                if (!byReference.TryGetValue(entry.Reference, out var product))
                {
                    product = new Product { ClientId = clientId, Reference = entry.Reference };
                    db.Products.Add(product);
                }
                product.Name = entry.Name;
                product.LengthMm = entry.LengthMm;
                product.WidthMm = entry.WidthMm;
                product.HeightMm = entry.HeightMm;
                product.WeightG = entry.WeightG;
                product.Price = entry.Price is { } p ? Money.FromMad(p) : null;
                product.Value = entry.Value is { } v ? Money.FromMad(v) : null;
                product.InternalStock = entry.InternalStock;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        return new ProductImportPreviewDto(table.Rows.Count, parsed.Count, errors.Count == 0 ? created : 0, errors.Count == 0 ? updated : 0, errors,
            parsed.Take(20).Select(p => new ProductImportSampleDto(p.Reference, p.Name, p.Price is { } price ? Money.MadToCents(price) : null)).ToList());
    }
}
