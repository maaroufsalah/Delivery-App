using System.Text.Json.Nodes;

namespace Delivery.Application.Settings;

/// <summary>`DEFAULT_BRAND_PALETTES` de `@delivery/shared/brand-palettes.ts`.</summary>
public static class BrandPalettes
{
    private const string Json = """
        [{"key":"orange","label":"Orange","primary":"#F05A28","secondary":"#1F1F1F"},{"key":"cobalt","label":"Bleu cobalt","primary":"#0F4C81","secondary":"#0B1F3A"},{"key":"navy","label":"Bleu nuit","primary":"#1E3A5F","secondary":"#0E1B2E"},{"key":"turquoise","label":"Turquoise","primary":"#0AB4B0","secondary":"#0F3D3E"},{"key":"emerald","label":"Vert émeraude","primary":"#178F5F","secondary":"#0F2B21"},{"key":"olive","label":"Vert olive","primary":"#5B7F2B","secondary":"#1F2A14"},{"key":"carmine","label":"Rouge carmin","primary":"#C8102E","secondary":"#1A1A1A"},{"key":"coral","label":"Corail","primary":"#E8505B","secondary":"#2A1A1D"},{"key":"violet","label":"Violet","primary":"#6941C6","secondary":"#1F1235"},{"key":"amber","label":"Ambre","primary":"#C98A1B","secondary":"#2B2118"},{"key":"graphite","label":"Anthracite","primary":"#3A3A3A","secondary":"#111111"}]
        """;

    public static JsonArray Defaults() => JsonNode.Parse(Json)!.AsArray();
}
