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

/// <summary>`POST /orders/import` : une commande par ligne, les références déjà connues ignorées.</summary>
public sealed record ImportOrdersCommand : WarehouseImport, IRequest<OrderImportPreviewDto>;

public sealed class ImportOrdersValidator : WarehouseImportValidator<ImportOrdersCommand>;

internal sealed class ImportOrdersHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ImportOrdersCommand, OrderImportPreviewDto>
{
    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["reference"] = ["reference", "ref", "commande", "order", "externalref"],
        ["recipient"] = ["destinataire", "client", "nom", "recipient"],
        ["phone"] = ["telephone", "tel", "phone", "gsm"],
        ["city"] = ["ville", "villedestination", "city"],
        ["address"] = ["adresse", "address"],
        ["product"] = ["produit", "referenceproduit", "sku", "product"],
        ["quantity"] = ["quantite", "qte", "quantity"],
        ["price"] = ["prix", "prixunitaire", "price"],
    };

    private static readonly string[] Required = ["reference", "recipient", "phone", "city", "address", "product"];

    private sealed record Parsed(int Row, string ExternalRef, string RecipientName, string RecipientPhone, string CityId, string DeliveryAddress, string ProductId, int Quantity, long UnitPrice);

    public async Task<OrderImportPreviewDto> Handle(ImportOrdersCommand input, CancellationToken cancellationToken)
    {
        var clientId = await WarehouseImportRules.ClientAsync(db, caller, input.ClientId, "Choisissez l’e-commerçant des commandes.", cancellationToken);
        var table = SheetReader.Read(excel, input.Content!);
        var columns = WarehouseImportRules.Columns(table.RawHeader, Aliases);
        if (Required.Any(key => columns[key] < 0))
        {
            throw BusinessException.Validation("Colonnes attendues : reference, destinataire, telephone, ville, adresse, produit, puis quantite et prix.", new { field = "content" });
        }
        var cities = new Dictionary<string, string>();
        foreach (var city in await db.Cities.AsNoTracking().Where(c => c.DeletedAt == null).Select(c => new { c.Id, c.Name, c.Code }).ToListAsync(cancellationToken))
        {
            cities[WarehouseImportRules.NormalizeHeader(city.Code)] = city.Id;
            cities[WarehouseImportRules.NormalizeHeader(city.Name)] = city.Id;
        }
        var products = new Dictionary<string, (string Id, long Price)>();
        foreach (var p in await db.Products.AsNoTracking().Where(p => p.ClientId == clientId && p.DeletedAt == null && p.Reference != null).Select(p => new { p.Id, p.Reference, p.Price }).ToListAsync(cancellationToken))
        {
            products[p.Reference!.ToUpperInvariant()] = (p.Id, Money.ToCents(p.Price));
        }
        var known = (await db.Orders.AsNoTracking().Where(o => o.ClientId == clientId && o.ExternalRef != null).Select(o => o.ExternalRef!).ToListAsync(cancellationToken))
            .Select(r => r.ToUpperInvariant()).ToHashSet();
        var errors = new List<WarehouseImportError>();
        var parsed = new List<Parsed>();
        var seen = new HashSet<string>();
        var skipped = 0;
        for (var index = 0; index < table.Rows.Count; index++)
        {
            var cells = table.Rows[index];
            var row = index + 2;
            string Read(string key) => columns[key] >= 0 && columns[key] < cells.Count ? cells[columns[key]].Trim() : string.Empty;
            var externalRef = Read("reference");
            var recipientName = Read("recipient");
            if (externalRef.Length == 0 && recipientName.Length == 0) continue;
            if (externalRef.Length == 0) { errors.Add(new(row, "reference", "required", "Référence absente.")); continue; }
            var key = externalRef.ToUpperInvariant();
            if (known.Contains(key)) { skipped++; continue; }
            if (!seen.Add(key)) { errors.Add(new(row, "reference", "duplicate", $"Référence « {externalRef} » répétée dans le fichier.")); continue; }
            var phone = Phones.Normalize(Read("phone"));
            if (!Phones.Pattern.IsMatch(phone)) { errors.Add(new(row, "telephone", "invalid-phone", $"« {Read("phone")} » : chiffres et + uniquement (pas de lettre).")); continue; }
            if (!cities.TryGetValue(WarehouseImportRules.NormalizeHeader(Read("city")), out var cityId)) { errors.Add(new(row, "ville", "unknown-city", $"Ville « {Read("city")} » inconnue.")); continue; }
            if (!products.TryGetValue(Read("product").ToUpperInvariant(), out var product)) { errors.Add(new(row, "produit", "unknown-product", $"Produit « {Read("product")} » inconnu chez cet e-commerçant.")); continue; }
            var rawQuantity = Read("quantity");
            var quantity = WarehouseImportRules.JsNumber(rawQuantity.Length == 0 ? "1" : rawQuantity);
            if (!double.IsFinite(quantity) || Math.Floor(quantity) != quantity || quantity < 1) { errors.Add(new(row, "quantite", "invalid-number", "Quantité invalide.")); continue; }
            var rawPrice = Read("price");
            var priceValue = rawPrice.Length == 0 ? product.Price : WarehouseImportRules.JsNumber(rawPrice) * 100;
            if (!double.IsFinite(priceValue) || priceValue < 0) { errors.Add(new(row, "prix", "invalid-number", "Prix invalide.")); continue; }
            var price = rawPrice.Length == 0 ? product.Price : (long)Math.Floor(priceValue + 0.5);
            var address = Read("address");
            if (address.Length < 1) { errors.Add(new(row, "adresse", "required", "Adresse obligatoire.")); continue; }
            parsed.Add(new Parsed(row, externalRef, recipientName, phone, cityId, address, product.Id, (int)quantity, price));
        }
        var apply = input.Apply == true && errors.Count == 0 && parsed.Count > 0;
        if (apply)
        {
            var now = clock.UtcNow;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var entry in parsed)
            {
                var code = await DocumentCodes.ReserveAsync(db, OrderRows.Prefix, now, cancellationToken);
                var order = new Order
                {
                    Code = code, ClientId = clientId, Source = OrderSource.EXCEL, ExternalRef = entry.ExternalRef, RecipientName = entry.RecipientName,
                    RecipientPhone = entry.RecipientPhone, DestinationCityId = entry.CityId, DeliveryAddress = entry.DeliveryAddress,
                    TotalAmount = Money.FromCents(entry.UnitPrice * entry.Quantity), CreatedById = caller.UserId,
                };
                db.Orders.Add(order);
                db.OrderLines.Add(new OrderLine { OrderId = order.Id, ProductId = entry.ProductId, Quantity = entry.Quantity, UnitPrice = Money.FromCents(entry.UnitPrice) });
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return new OrderImportPreviewDto(table.Rows.Count, parsed.Count, input.Apply == true && errors.Count == 0 ? parsed.Count : 0, skipped, errors,
            parsed.Take(20).Select(p => new OrderImportSampleDto(p.ExternalRef, p.RecipientName, p.UnitPrice * p.Quantity)).ToList());
    }
}
