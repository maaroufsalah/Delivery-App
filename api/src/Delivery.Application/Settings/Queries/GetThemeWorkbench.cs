using System.Text.Json;
using System.Text.Json.Nodes;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Settings.Dtos;
using MediatR;

namespace Delivery.Application.Settings.Queries;

/// <summary>`GET /settings/theme` : palettes proposées et instantanés.</summary>
public sealed record GetThemeWorkbenchQuery : IRequest<ThemeWorkbenchDto>;

internal sealed class GetThemeWorkbenchHandler(IAppDbContext db) : IRequestHandler<GetThemeWorkbenchQuery, ThemeWorkbenchDto>
{
    public async Task<ThemeWorkbenchDto> Handle(GetThemeWorkbenchQuery request, CancellationToken cancellationToken)
    {
        var palettes = await db.SettingAsync(SettingKeys.ThemePresets, cancellationToken);
        var standard = await db.SettingAsync(SettingKeys.ThemeStandard, cancellationToken);
        var previous = await db.SettingAsync(SettingKeys.ThemePrevious, cancellationToken);
        JsonNode? Node(JsonElement? e) => e is null ? null : JsonNode.Parse(e.Value.GetRawText());
        var list = palettes is { ValueKind: JsonValueKind.Array } p && p.GetArrayLength() > 0 ? Node(p)! : BrandPalettes.Defaults();
        return new ThemeWorkbenchDto(list, Node(standard), Node(previous));
    }
}
