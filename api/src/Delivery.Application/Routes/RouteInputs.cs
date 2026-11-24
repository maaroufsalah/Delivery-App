using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.BulkJobs;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>Les nombres du contrat Zod des circuits (`z.number()` : un nombre JSON, pas une chaîne).</summary>
internal static class RouteInputs
{
    public static int? Int(JsonElement? value) => value is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out var i) ? i : null;

    public static double? Number(JsonElement? value) => value is { ValueKind: JsonValueKind.Number } v ? v.GetDouble() : null;

    public static void PositiveInt(JsonElement? value, string path, Action<string, string> issue)
    {
        if (value is null) return;
        if (value.Value.ValueKind != JsonValueKind.Number) issue(ValidationPaths.Raw + path, Zod.Expected("number", TypeOf(value.Value)));
        else if (Math.Floor(value.Value.GetDouble()) != value.Value.GetDouble()) issue(ValidationPaths.Raw + path, Zod.ExpectedInt);
        else if (value.Value.GetDouble() <= 0) issue(ValidationPaths.Raw + path, "Too small: expected number to be >0");
    }

    private static string TypeOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "array",
        _ => "object",
    };

    public static void Amount(JsonElement? value, string path, Action<string, string> issue)
    {
        if (value is null) return;
        if (value.Value.ValueKind != JsonValueKind.Number) issue(ValidationPaths.Raw + path, Zod.Expected("number", TypeOf(value.Value)));
        else if (value.Value.GetDouble() < 0) issue(ValidationPaths.Raw + path, "negative");
        else if (value.Value.GetDouble() > 1_000_000) issue(ValidationPaths.Raw + path, "amount-too-large");
    }

    public static void Range(JsonElement? value, string path, double min, double max, Action<string, string> issue)
    {
        if (value is null) return;
        if (value.Value.ValueKind != JsonValueKind.Number) issue(ValidationPaths.Raw + path, Zod.Expected("number", TypeOf(value.Value)));
        else if (value.Value.GetDouble() < min) issue(ValidationPaths.Raw + path, Zod.TooSmallNumber((decimal)min));
        else if (value.Value.GetDouble() > max) issue(ValidationPaths.Raw + path, Zod.TooBigNumber((decimal)max));
    }
}
