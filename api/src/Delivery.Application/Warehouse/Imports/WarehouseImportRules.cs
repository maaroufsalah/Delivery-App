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

public sealed record WarehouseImportError(int Row, string? Column, string Code, string Message);

/// <summary>`warehouseImportSchema` : un classeur, aperçu ou application.</summary>
public abstract record WarehouseImport
{
    public string? Content { get; init; }
    public string? Filename { get; init => field = Zod.Trim(value); }
    public bool? Apply { get; init; } = false;
    public string? ClientId { get; init; }
}

public abstract class WarehouseImportValidator<T> : ZodValidator<T>
    where T : WarehouseImport
{
    protected WarehouseImportValidator()
    {
        RuleFor(x => x.Content).Required().MinLength(1, "required");
        RuleFor(x => x.Filename).MaxLength(255);
        RuleFor(x => x.ClientId).Must(v => v is null || v.Length >= 1 || v == string.Empty).WithMessage(Zod.InvalidInput);
    }
}

public static class WarehouseImportRules
{
    /// <summary>`normalizeHeader` : sans accents, en minuscules, lettres et chiffres seulement.</summary>
    public static string NormalizeHeader(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            if (ch is >= '̀' and <= 'ͯ') continue;
            var lower = char.ToLowerInvariant(ch);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9') builder.Append(lower);
        }
        return builder.ToString();
    }

    public static Dictionary<string, int> Columns(IReadOnlyList<string> rawHeader, IReadOnlyDictionary<string, string[]> aliases)
    {
        var header = rawHeader.Select(NormalizeHeader).ToList();
        return aliases.ToDictionary(a => a.Key, a => header.FindIndex(name => a.Value.Contains(name)));
    }

    public static async Task<string> ClientAsync(IAppDbContext db, ICurrentUser caller, string? requested, string missing, CancellationToken cancellationToken)
    {
        if (WarehouseScope.IsMerchant(caller)) return caller.ClientId!;
        if (string.IsNullOrEmpty(requested)) throw BusinessException.Validation(missing, new { field = "clientId" });
        return await db.Clients.AsNoTracking().Where(c => c.Id == requested && c.DeletedAt == null).Select(c => c.Id).FirstOrDefaultAsync(cancellationToken)
               ?? throw BusinessException.NotFound("E-commerçant introuvable.");
    }

    /// <summary>`Number(text)` de JS, virgule décimale tolérée, espaces retirés.</summary>
    public static double JsNumber(string raw)
    {
        var comma = raw.IndexOf(',', StringComparison.Ordinal);
        var text = comma >= 0 ? string.Concat(raw.AsSpan(0, comma), ".", raw.AsSpan(comma + 1)) : raw;
        text = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (text.Length == 0) return 0;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
    }
}

/// <summary>`ProductImportPreview`.</summary>
public sealed record ProductImportPreviewDto(int Rows, int Valid, int Created, int Updated, IReadOnlyList<WarehouseImportError> Errors, IReadOnlyList<ProductImportSampleDto> Sample);

public sealed record ProductImportSampleDto(string Reference, string Name, long? Price);

/// <summary>`OrderImportPreview`.</summary>
public sealed record OrderImportPreviewDto(int Rows, int Valid, int Created, int Skipped, IReadOnlyList<WarehouseImportError> Errors, IReadOnlyList<OrderImportSampleDto> Sample);

public sealed record OrderImportSampleDto(string ExternalRef, string RecipientName, long TotalAmount);
