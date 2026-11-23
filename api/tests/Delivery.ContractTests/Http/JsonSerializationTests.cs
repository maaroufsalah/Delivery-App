using System.Text.Json;
using Delivery.Api.Http;

namespace Delivery.ContractTests.Http;

public sealed class JsonSerializationTests
{
    private static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions();
        JsonDefaults.Apply(options);
        return options;
    }

    private sealed record Sample(decimal Amount, DateTime At, DateOnly Day, DayOfWeek Kind, string? Missing);

    [Fact]
    public void Matches_json_stringify_of_a_prisma_row()
    {
        var json = JsonSerializer.Serialize(
            new Sample(12.50m, new DateTime(2026, 1, 31, 10, 0, 0, 120, DateTimeKind.Utc), new DateOnly(2026, 2, 1), DayOfWeek.Monday, null),
            Options);
        json.ShouldBe("{\"amount\":12.5,\"at\":\"2026-01-31T10:00:00.120Z\",\"day\":\"2026-02-01T00:00:00.000Z\",\"kind\":\"Monday\",\"missing\":null}");
    }

    [Theory]
    [InlineData("0.00", "0")]
    [InlineData("-0.00", "0")]
    [InlineData("100.10", "100.1")]
    [InlineData("1234567.89", "1234567.89")]
    public void Decimals_are_written_like_js_numbers(string input, string expected) =>
        JsNumberDecimalConverter.Normalize(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);
}
