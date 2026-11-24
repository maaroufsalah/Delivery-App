using System.Globalization;
using System.Text.RegularExpressions;
using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Pricing.Dtos;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Pricing.Commands;

/// <summary>`POST /pricing-rules/by-origin/:originCityId/import` : aperçu, ou application avec `apply`.</summary>
public sealed record ImportPricingOriginCommand : IRequest<PricingOriginImportPreviewDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string OriginCityId { get; init; } = string.Empty;

    public string? Filename { get; init => field = Zod.Trim(value); }

    public string? Content { get; init; }

    public bool? Apply { get; init; } = false;
}

public sealed class ImportPricingOriginValidator : ZodValidator<ImportPricingOriginCommand>
{
    public ImportPricingOriginValidator()
    {
        RuleFor(x => x.Filename).MaxLength(200);
        RuleFor(x => x.Content).Required().MinLength(1, "required").MaxLength(4_000_000, "file-too-large");
    }
}

internal sealed partial class ImportPricingOriginHandler(IAppDbContext db, IClock clock, IExcelService excel) : IRequestHandler<ImportPricingOriginCommand, PricingOriginImportPreviewDto>
{
    private static readonly string[] CityColumns = ["ville", "destination", "city"];
    private static readonly string[] CodeColumns = ["code"];
    private static readonly string[] RateColumns = ["tarif", "prix", "rate", "quartier"];
    private static readonly string[] DelayColumns = ["delai", "delay"];
    private static readonly Regex RatePattern = new(@"^(\d+(?:[.,]\d+)?)(?:dh|mad)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<PricingOriginImportPreviewDto> Handle(ImportPricingOriginCommand input, CancellationToken cancellationToken)
    {
        var table = SheetReader.Read(excel, input.Content!);
        var header = table.RawHeader.Select(CityNames.NormalizeHeader).ToList();
        int Find(string[] names) => header.FindIndex(names.Contains);
        var (city, code, rate, delay) = (Find(CityColumns), Find(CodeColumns), Find(RateColumns), Find(DelayColumns));
        if (city < 0 || rate < 0) throw BusinessException.Validation("Colonnes obligatoires absentes : Ville, Tarif.", new { found = table.RawHeader });

        var page = await PricingGrid.ByOriginAsync(db, input.OriginCityId, new ListQuery { Page = 1, PageSize = 100_000, Sort = "name" }, null, cancellationToken);
        var byCode = new Dictionary<string, PricingOriginRowDto>();
        var byKey = new Dictionary<string, PricingOriginRowDto>();
        foreach (var row in page.Items)
        {
            byCode[row.Code.ToUpperInvariant()] = row;
            byKey[CityNames.CityKey(row.Name)] = row;
        }

        var recognized = new List<PricingOriginImportRowDto>();
        var writes = new List<(PricingOriginRowInput Row, bool Changed)>();
        var rejected = new List<ImportRowError>();
        var seen = new Dictionary<string, int>();
        for (var index = 0; index < table.Rows.Count; index++)
        {
            var cells = table.Rows[index];
            string Cell(int column) => column >= 0 && column < cells.Count ? cells[column] : string.Empty;
            var number = index + 2;
            var raw = Cell(city).Trim();
            var cityCode = code < 0 ? string.Empty : Cell(code).Trim().ToUpperInvariant();
            if (raw.Length == 0 && cityCode.Length == 0) continue;
            var target = byCode.GetValueOrDefault(cityCode) ?? byCode.GetValueOrDefault(raw.ToUpperInvariant()) ?? byKey.GetValueOrDefault(CityNames.CityKey(CityNames.Normalize(raw).Name));
            if (target is null)
            {
                rejected.Add(new ImportRowError(number, "ville", "unknown-city", $"Ville inconnue : « {(raw.Length > 0 ? raw : cityCode)} ». Aucune ville n’est créée par un import de tarifs."));
                continue;
            }
            if (seen.TryGetValue(target.CityId, out var twin))
            {
                rejected.Add(new ImportRowError(number, "ville", "duplicate-city", $"{target.Name} figure déjà ligne {twin} : la première ligne l’emporte."));
                continue;
            }
            var rateCell = Cell(rate).Trim();
            double? amount = null;
            if (rateCell.Length > 0)
            {
                amount = ParseRate(rateCell);
                if (amount is null)
                {
                    rejected.Add(new ImportRowError(number, "tarif", "not-a-number", $"« {rateCell} » n’est pas un tarif lisible."));
                    continue;
                }
            }
            seen[target.CityId] = number;
            var delayCell = delay < 0 ? string.Empty : Regex.Replace(Cell(delay), @"\s+", " ").Trim();
            var deliveryDelay = delayCell.Length == 0 ? null : delayCell[..Math.Min(40, delayCell.Length)];
            long? rateCents = amount is null ? null : Money.MadToCents(amount.Value);
            var changed = (rateCents is not null && rateCents != target.Rate) || (deliveryDelay is not null && deliveryDelay != target.DeliveryDelay);
            recognized.Add(new PricingOriginImportRowDto(number, raw, target.CityId, target.Name, target.Code, rateCents, target.Rate, deliveryDelay, target.DeliveryDelay, changed));
            var keep = target.Rate is null ? (double?)null : target.Rate.Value / 100d;
            var save = deliveryDelay is null
                ? new PricingOriginRowInput { DestinationCityId = target.CityId, Rate = amount ?? keep }
                : new PricingOriginRowInput { DestinationCityId = target.CityId, Rate = amount ?? keep, DeliveryDelay = deliveryDelay };
            writes.Add((save, changed));
        }

        var toWrite = writes.Where(w => w.Changed).Select(w => w.Row).ToList();
        if (input.Apply == true && toWrite.Count > 0) await PricingGrid.SaveOriginAsync(db, clock, input.OriginCityId, toWrite, cancellationToken);
        return new PricingOriginImportPreviewDto(page.Origin, table.Rows.Count, recognized, rejected.Take(500).ToList(), recognized.Count(r => r.Changed));
    }

    private static double? ParseRate(string value)
    {
        var match = RatePattern.Match(Regex.Replace(value, @"\s", string.Empty));
        if (!match.Success) return null;
        var amount = double.Parse(match.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        return double.IsFinite(amount) && amount <= 1_000_000 ? amount : null;
    }
}
