using System.IO.Compression;
using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Settings;
using Delivery.Application.Settings;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Labels;

/// <summary>`LabelService` : les colis d'une planche, la marque, le rendu (PDF ou ZIP de PDF).</summary>
public static class ParcelLabels
{
    public const int SelectionMax = 5_000;
    public const int AsyncThreshold = 500;
    public const int BatchMax = 200;
    public const int SinglePdfMax = 1_000;
    public const int ChunkSize = 500;
    public const int JobTtlHours = 24;

    /// <summary>`COURIER_STOCK_JOB_FORMAT` : le « format » d'un travail qui porte la situation du stock livreur.</summary>
    public const string StockJobFormat = "stock";
    public static readonly string[] Formats = [LabelSheet.Format10X10, LabelSheet.FormatA4];

    private static readonly Dictionary<DeliveryMode, string> DeliveryModeLabels = new()
    {
        [DeliveryMode.DISTRICT] = "À domicile",
        [DeliveryMode.AGENCY] = "En agence",
        [DeliveryMode.RELAY_POINT] = "Point relais",
    };

    /// <summary>`load` : les colis visibles, rangés par quartier puis code, au plus <see cref="SelectionMax"/>.</summary>
    public static async Task<List<LabelData>> LoadAsync(IQueryable<Parcel> scoped, CancellationToken cancellationToken)
    {
        var rows = await scoped.Where(p => p.DeletedAt == null)
            .OrderBy(p => p.DestinationDistrictId).ThenBy(p => p.Code).Take(SelectionMax)
            .Select(p => new
            {
                p.Code, p.ExternalCode, p.RecipientName, p.RecipientPhone, p.DeliveryAddress, p.DestinationDistrictName,
                DistrictName = p.DestinationDistrict != null ? p.DestinationDistrict.Name : null,
                City = p.DestinationCity.Name, CityAr = p.DestinationCity.NameAr, CityCode = p.DestinationCity.Code, ZoneName = p.DestinationCity.Zone.Name,
                ClientName = p.Client.Name, ClientPhone = p.Client.Phone, AgencyCode = p.Agency.City.Code,
                p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid, p.Description, p.Quantity, p.ExpectedPayment, p.DeliveryMode, p.OpenOnDelivery, p.CreatedAt,
            }).ToListAsync(cancellationToken);
        return rows.Select(p =>
        {
            var district = ParcelViews.DistrictLabel(p.DestinationDistrictName, p.DistrictName);
            var toCollect = FinancialStatuses.CodToCollect(Money.ToCents(p.CodAmount), Money.ToCents(p.DeliveryFee), p.FeeIncluded, p.AlreadyPaid);
            return new LabelData
            {
                Code = p.Code,
                ExternalCode = p.ExternalCode,
                RecipientName = p.RecipientName,
                RecipientPhone = p.RecipientPhone,
                DeliveryAddress = p.DeliveryAddress,
                DestinationCityName = p.City,
                DestinationDistrictName = district,
                ClientName = p.ClientName,
                CodToCollect = toCollect,
                Description = p.Description,
                Quantity = p.Quantity,
                ExpectedPaymentLabel = p.ExpectedPayment is { } expected && !p.AlreadyPaid ? $"Paiement : {(expected == ExpectedPayment.CHEQUE ? "Chèque" : "Espèces")}" : null,
                AgencyCode = p.AgencyCode,
                ZoneName = p.ZoneName,
                DestinationCityCode = p.CityCode,
                DestinationCityNameAr = string.IsNullOrEmpty(p.CityAr) ? null : p.CityAr,
                DestinationLocality = string.Join(", ", new[] { district, p.City }.Where(s => !string.IsNullOrEmpty(s))),
                ClientPhone = p.ClientPhone,
                CreatedAt = Exports.DateText(p.CreatedAt),
                DeliveryModeLabel = DeliveryModeLabels.GetValueOrDefault(p.DeliveryMode, "À domicile"),
                PaymentModeLabel = toCollect == 0 ? "Déjà payé" : "Espèces / Chèque",
                OpenOnDelivery = p.OpenOnDelivery,
            };
        }).ToList();
    }

    /// <summary>`payload` : modèle (classique ou à la marque) et format de la planche.</summary>
    public static async Task<LabelSheet> SheetAsync(IAppDbContext db, IFileStorage storage, IReadOnlyList<LabelData> parcels, string format, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.Id, c.Name, c.LogoUrl, c.PrimaryColor }).FirstOrDefaultAsync(cancellationToken);
        var template = await TextSettingAsync(db, SettingKeys.LabelTemplate, LabelSheet.Classic, cancellationToken);
        var branded = template == LabelSheet.Branded;
        LabelBrand? brand = null;
        if (branded)
        {
            var color = await TextSettingAsync(db, SettingKeys.LabelBrandColor, string.Empty, cancellationToken);
            var banner = (await TextSettingAsync(db, SettingKeys.LabelBanner, string.Empty, cancellationToken)).Trim();
            byte[]? logo = null;
            if (company is not null && Logos.KeyOf(company.Id, company.LogoUrl) is { } key)
            {
                try
                {
                    logo = await storage.GetAsync(key, cancellationToken);
                }
                catch (BusinessException)
                {
                    logo = null;
                }
            }
            var chosen = (string.IsNullOrEmpty(color) ? company?.PrimaryColor ?? "#111111" : color).Trim();
            brand = new LabelBrand(company?.Name ?? string.Empty, string.IsNullOrEmpty(chosen) ? "#111111" : chosen, logo, banner.Length == 0 ? null : banner);
        }
        return new LabelSheet(branded ? LabelSheet.Branded : LabelSheet.Classic, format, company?.Name ?? "Étiquettes", brand, parcels);
    }

    /// <summary>`LabelRenderer.render` : un PDF, ou un ZIP de PDF de <see cref="ChunkSize"/> étiquettes au-delà de <see cref="SinglePdfMax"/>.</summary>
    public static (byte[] Content, bool Zip) Render(IPdfService pdf, LabelSheet sheet)
    {
        var labels = sheet.Expanded().Select(l => l with { Quantity = 1 }).ToList();
        if (labels.Count <= SinglePdfMax) return (pdf.Render(sheet), false);
        var step = Math.Max(4, ChunkSize - (ChunkSize % 4));
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int at = 0, index = 0; at < labels.Count; at += step, index++)
            {
                var chunk = labels.Skip(at).Take(step).ToList();
                var first = index * ChunkSize + 1;
                var entry = zip.CreateEntry($"etiquettes-{first:0000}-{first + chunk.Count - 1:0000}.pdf", CompressionLevel.NoCompression);
                using var stream = entry.Open();
                stream.Write(pdf.Render(sheet with { Parcels = chunk }));
            }
        }
        return (buffer.ToArray(), true);
    }

    public static FileContent Pdf(byte[] content, string fileName) => new(content, "application/pdf", fileName) { CacheControl = "private, no-store" };

    /// <summary>Ordre d'une sélection : celui des codes demandés.</summary>
    public static List<LabelData> InOrder(IEnumerable<LabelData> parcels, IReadOnlyList<string> codes)
    {
        var rank = codes.Select((code, index) => (code, index)).GroupBy(x => x.code).ToDictionary(g => g.Key, g => g.First().index);
        return parcels.OrderBy(p => rank.GetValueOrDefault(p.Code)).ToList();
    }

    private static async Task<string> TextSettingAsync(IAppDbContext db, string key, string fallback, CancellationToken cancellationToken)
    {
        var value = await db.SettingAsync(key, cancellationToken);
        return value is { ValueKind: JsonValueKind.String } text ? text.GetString() ?? fallback : fallback;
    }
}
