using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Labels;

/// <summary>`labelSelectionSchema` : des codes, ou « tout » (filtres de la liste) moins des exclus.</summary>
public abstract record LabelSelection
{
    public IReadOnlyList<string>? Codes { get; init => field = value?.Select(c => c?.Trim() ?? string.Empty).ToList(); }

    public bool? All { get; init; }

    public IReadOnlyList<string>? Exclude { get; init => field = value?.Select(c => c?.Trim() ?? string.Empty).ToList(); }

    public ParcelListParams? Filters { get; init; }

    public string? Format { get; init; } = LabelSheet.Format10X10;
}

public abstract class LabelSelectionValidator<T> : ZodValidator<T>
    where T : LabelSelection
{
    protected LabelSelectionValidator()
    {
        RuleFor(x => x.Codes).Custom((codes, context) =>
        {
            if (codes is null) return;
            for (var i = 0; i < codes.Count; i++) if (codes[i].Length < 1) context.AddFailure(ValidationPaths.Raw + $"codes.{i}", Zod.TooSmallString(1));
            if (codes.Count > ParcelLabels.SelectionMax) context.AddFailure(ValidationPaths.Raw + "codes", "too-many");
        });
        RuleFor(x => x.Exclude).Custom((codes, context) =>
        {
            if (codes is null) return;
            for (var i = 0; i < codes.Count; i++) if (codes[i].Length < 1) context.AddFailure(ValidationPaths.Raw + $"exclude.{i}", Zod.TooSmallString(1));
            if (codes.Count > ParcelLabels.SelectionMax) context.AddFailure(ValidationPaths.Raw + "exclude", "too-many");
        });
        RuleFor(x => x.Format).OneOf(ParcelLabels.Formats);
    }

    protected override void Refine(T value, Action<string, string> issue)
    {
        if (value.All != true && (value.Codes?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "codes", "required");
    }
}

/// <summary>`labelCodes` : les codes de la sélection, dans l'ordre de la liste.</summary>
public static class LabelSelections
{
    public static async Task<IReadOnlyList<string>> CodesAsync(IAppDbContext db, ICurrentUser caller, IClock clock, LabelSelection input, CancellationToken cancellationToken)
    {
        var filter = input.All == true ? (input.Filters ?? new ParcelListParams()).ToFilter(withPaging: false) : new ParcelListFilter();
        var (where, _) = await ParcelFilters.InPeriodAsync(db, caller, filter, clock, cancellationToken);
        if (input.All == true)
        {
            if (input.Exclude is { Count: > 0 } exclude) where = where.Where(p => !exclude.Contains(p.Code));
        }
        else
        {
            var codes = input.Codes ?? [];
            where = where.Where(p => codes.Contains(p.Code));
        }
        var rows = await where.OrderByDescending(p => p.StatusAt).ThenBy(p => p.Code).Select(p => p.Code).Take(ParcelLabels.SelectionMax + 1).ToListAsync(cancellationToken);
        if (rows.Count > ParcelLabels.SelectionMax)
        {
            throw BusinessException.Validation($"Plus de {ParcelLabels.SelectionMax} colis : affinez les filtres pour imprimer les étiquettes.", new { reason = "selection-too-large", max = ParcelLabels.SelectionMax });
        }
        if (rows.Count == 0) throw BusinessException.NotFound("Aucun de ces colis ne vous est accessible.");
        return rows;
    }
}

/// <summary>`LabelJob` sur le fil.</summary>
public sealed record LabelJobDto(string Id, string Status, string Format, int Count, DateTime CreatedAt, DateTime? FinishedAt, DateTime ExpiresAt, int? SizeBytes, string FileType, string? Error)
{
    public static LabelJobDto From(LabelJob row) => new(row.Id, row.Status.ToString().ToLowerInvariant(), row.Format, row.ParcelCount, row.CreatedAt, row.FinishedAt, row.ExpiresAt,
        row.SizeBytes, row.StorageKey?.EndsWith(".zip", StringComparison.Ordinal) == true ? "zip" : "pdf", row.Error);
}

internal static class LabelJobFiles
{
    public static async Task<LabelJob> OwnAsync(IAppDbContext db, ICurrentUser caller, IClock clock, string id, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return await db.LabelJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.RequestedById == caller.UserId && j.ExpiresAt > now, cancellationToken)
               ?? throw BusinessException.NotFound("Étiquettes introuvables ou expirées.");
    }
}
