using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Delivery.Api.Http;

/// <summary>
/// Sérialisation identique à `JSON.stringify` côté Nest : dates ISO en UTC à la
/// milliseconde (`2026-01-31T10:00:00.000Z`), décimaux comme un `Number` JS.
/// </summary>
public static class JsonDefaults
{
    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = null;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new IsoDateTimeConverter());
        options.Converters.Add(new IsoDateOnlyConverter());
        options.Converters.Add(new JsNumberDecimalConverter());
    }
}

public sealed class IsoDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString() ?? throw new JsonException("Date attendue.");
        return DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Format(value));

    public static string Format(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }
}

/// <summary>Un `@db.Date` Prisma revient en `Date` JS à minuit UTC.</summary>
public sealed class IsoDateOnlyConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString() ?? throw new JsonException("Date attendue.");
        return DateOnly.FromDateTime(DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00.000Z");
}

/// <summary>`12.50m` s'écrit `12.5`, comme `Number('12.50')` en JS.</summary>
public sealed class JsNumberDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? decimal.Parse(reader.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture)
            : reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteRawValue(Normalize(value), skipInputValidation: true);

    public static string Normalize(decimal value)
    {
        var text = value.ToString("0.############################", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }
}
