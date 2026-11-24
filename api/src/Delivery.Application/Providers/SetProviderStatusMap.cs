using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Providers;

/// <summary>`PUT /settings/providers/:id/status-map` (`providerStatusMapSchema`).</summary>
public sealed record SetProviderStatusMapCommand : IRequest<ProviderTokenListItemDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public JsonNode? StatusMap { get; init; }
}

public sealed class SetProviderStatusMapValidator : ZodValidator<SetProviderStatusMapCommand>
{
    public SetProviderStatusMapValidator()
    {
        RuleFor(x => x.StatusMap).Custom((map, context) =>
        {
            if (map is not JsonObject entries)
            {
                context.AddFailure(ValidationPaths.Raw + "statusMap", map is null ? "Invalid input: expected record, received undefined" : Zod.Expected("record", JsKind(map)));
                return;
            }
            var typed = true;
            foreach (var (key, value) in entries)
            {
                if (value is null) continue;
                if (value.GetValueKind() != JsonValueKind.Number) { context.AddFailure(ValidationPaths.Raw + $"statusMap.{key}", Zod.Expected("number", JsKind(value))); typed = false; continue; }
                var number = value.GetValue<double>();
                if (Math.Floor(number) != number) { context.AddFailure(ValidationPaths.Raw + $"statusMap.{key}", Zod.ExpectedInt); typed = false; }
                else if (number <= 0) { context.AddFailure(ValidationPaths.Raw + $"statusMap.{key}", "Too small: expected number to be >0"); typed = false; }
            }
            if (!typed) return;
            foreach (var (key, value) in entries)
            {
                if (!int.TryParse(key, out var id) || !ProviderStatusMap.ProviderStatusIds.Contains(id)) context.AddFailure(ValidationPaths.Raw + $"statusMap.{key}", "unknown-provider-status");
                if (value is not null && StatusCatalog.Find((int)value.GetValue<double>()) is null) context.AddFailure(ValidationPaths.Raw + $"statusMap.{key}", "unknown-target-status");
            }
        });
    }

    private static string JsKind(JsonNode? node) => node?.GetValueKind() switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Array => "array",
        JsonValueKind.Null or null => "null",
        _ => "object",
    };
}

internal sealed class SetProviderStatusMapHandler(IAppDbContext db) : IRequestHandler<SetProviderStatusMapCommand, ProviderTokenListItemDto>
{
    public async Task<ProviderTokenListItemDto> Handle(SetProviderStatusMapCommand input, CancellationToken cancellationToken)
    {
        var row = await ProviderTokens.FindAsync(db, input.Id, cancellationToken);
        row.StatusMap = input.StatusMap!.ToJsonString();
        await db.SaveChangesAsync(cancellationToken);
        return await ProviderTokens.ItemAsync(db, row.Id, cancellationToken);
    }
}
