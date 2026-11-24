namespace Delivery.Application.Common.Documents;

/// <summary>`LabelCarton` : « carton i/N » d'un colis de plusieurs cartons.</summary>
public sealed record LabelCarton(int Index, int Total);

/// <summary>`BrandedLabelData` : tout ce qu'imprime une étiquette, classique ou à la marque.</summary>
public sealed record LabelData
{
    public string Code { get; init; } = string.Empty;
    public string? ExternalCode { get; init; }
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientPhone { get; init; } = string.Empty;
    public string DeliveryAddress { get; init; } = string.Empty;
    public string DestinationCityName { get; init; } = string.Empty;
    public string? DestinationDistrictName { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public long CodToCollect { get; init; }
    public string? Description { get; init; }
    public int Quantity { get; init; } = 1;
    public string? ExpectedPaymentLabel { get; init; }
    public string AgencyCode { get; init; } = string.Empty;
    public string ZoneName { get; init; } = string.Empty;
    public string DestinationCityCode { get; init; } = string.Empty;
    public string? DestinationCityNameAr { get; init; }
    public string DestinationLocality { get; init; } = string.Empty;
    public string ClientPhone { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string DeliveryModeLabel { get; init; } = string.Empty;
    public string PaymentModeLabel { get; init; } = string.Empty;
    public bool OpenOnDelivery { get; init; }
    public string? Comment { get; init; }
    public LabelCarton? Carton { get; init; }
}

/// <summary>`LabelBrand` : la marque de la société sur le modèle « branded ».</summary>
public sealed record LabelBrand(string Name, string Color, byte[]? Logo, string? Banner)
{
    public string? OnBrandColor { get; init; }

    /// <summary>`logoOnLight` : le logo préparé pour la bande claire (un logo blanc y est noirci).</summary>
    public byte[]? LogoOnLight { get; init; }
}

/// <summary>`LabelRenderPayload` : le document d'étiquettes à rendre.</summary>
public sealed record LabelSheet(string Template, string Format, string CompanyName, LabelBrand? Brand, IReadOnlyList<LabelData> Parcels)
{
    public const string Classic = "classic";
    public const string Branded = "branded";
    public const string Format10X10 = "10x10";
    public const string FormatA4 = "a4";

    /// <summary>`expandCartons` : une étiquette par carton.</summary>
    public IReadOnlyList<LabelData> Expanded() =>
        Parcels.SelectMany(parcel =>
        {
            var total = Math.Max(parcel.Quantity, 1);
            return total == 1 ? [parcel] : Enumerable.Range(1, total).Select(i => parcel with { Carton = new LabelCarton(i, total) });
        }).ToList();

    /// <summary>`labelPageCount`.</summary>
    public static int PageCount(IEnumerable<int> quantities) => quantities.Sum(q => Math.Max(q, 1));
}

/// <summary>Formats communs aux documents PDF (`styles.ts`).</summary>
public static class PdfText
{
    /// <summary>`formatAmount` : « 199,90 MAD ».</summary>
    public static string Amount(long cents)
    {
        var sign = cents < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(cents);
        return $"{sign}{absolute / 100},{absolute % 100:00} MAD";
    }

    /// <summary>`truncate` : coupé à <paramref name="max"/> caractères avec « … ».</summary>
    public static string Truncate(string value, int max) => value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");

    public static string? CartonMention(LabelData parcel) => parcel.Carton is { } carton ? $"{carton.Index}/{carton.Total}" : null;
}
