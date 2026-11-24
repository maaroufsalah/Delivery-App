using System.Globalization;

namespace Delivery.Domain.Common;

/// <summary>`common/wire.ts` : la base garde des `Decimal(12,2)`, le fil des centimes entiers.</summary>
public static class Money
{
    /// <summary>`toCents` : `Math.round(Number(value) * 100)`, 0 si absent.</summary>
    public static long ToCents(decimal? value) => value is null ? 0 : (long)Math.Floor(value.Value * 100m + 0.5m);

    public static long? ToCentsOrNull(decimal? value) => value is null ? null : ToCents(value);

    /// <summary>`fromCents` : centimes → décimal à deux chiffres.</summary>
    public static decimal FromCents(long cents) => cents / 100m;

    /// <summary>`fromCents(Math.round(mad * 100))` : un montant saisi en MAD (nombre JS).</summary>
    public static decimal FromMad(double mad) => FromCents((long)Math.Floor((mad * 100) + 0.5));

    /// <summary>`Math.round(mad * 100)` sur un nombre JS.</summary>
    public static long MadToCents(double mad) => (long)Math.Floor((mad * 100) + 0.5);

    /// <summary>`toWireEnum` : `TO_INVOICE` → `to-invoice`.</summary>
    public static string ToWire(string value) => value.ToLowerInvariant().Replace('_', '-');

    public static string ToWire<T>(T value) where T : struct, Enum => ToWire(value.ToString());

    /// <summary>`fromWireEnum` : `to-invoice` → `TO_INVOICE`.</summary>
    public static string FromWire(string value) => value.ToUpperInvariant().Replace('-', '_');

    public static string Format(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
