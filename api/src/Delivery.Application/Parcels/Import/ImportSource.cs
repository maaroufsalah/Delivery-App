using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Validation;
using FluentValidation;

namespace Delivery.Application.Parcels.Import;

/// <summary>`parcelImportSourceSchema` : un classeur (base64) ou des lignes, l'un ou l'autre.</summary>
public abstract record ImportSource
{
    public string? Content { get; init; }

    public IReadOnlyList<ParcelImportRowInput>? Rows { get; init; }

    public string? Filename { get; init => field = Zod.Trim(value); }

    public string? ClientId { get; init; }
}

public abstract class ImportSourceValidator<T> : ZodValidator<T>
    where T : ImportSource
{
    protected ImportSourceValidator()
    {
        RuleFor(x => x.Content).MaxLength(4_000_000, "file-too-large");
        RuleFor(x => x.Rows).Must(r => r is null || r.Count <= ParcelImports.MaxRows).WithMessage("too-many-rows");
        RuleFor(x => x.Filename).MaxLength(200);
    }

    protected override void Refine(T value, Action<string, string> issue)
    {
        if (!string.IsNullOrEmpty(value.Content) == (value.Rows is not null)) issue(ValidationPaths.Raw + "content", "required");
    }
}

/// <summary>`ParcelImportPreview`.</summary>
public sealed record ParcelImportPreviewDto(string ClientId, string ClientName, string OriginCityName, int Rows, int Valid, int Invalid, IReadOnlyList<ParcelImportRowDto> Items, long EstimatedFees);

/// <summary>`ParcelImportResult`.</summary>
public sealed record ParcelImportResultDto(string ImportId, int Created, int Skipped, int Warnings, IReadOnlyList<string> Codes);

/// <summary>`ParcelImportListItem`.</summary>
public sealed record ParcelImportListItemDto(string Id, string Filename, string ClientId, string ClientName, string AuthorName, string Mode, int RowCount, int OkCount, int KoCount, DateTime CreatedAt);
