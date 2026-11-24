using System.Text.Json;
using System.Text.Json.Nodes;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Settings.Dtos;
using MediatR;

namespace Delivery.Application.Settings.Queries;

/// <summary>`GET /settings/business`.</summary>
public sealed record GetBusinessSettingsQuery : IRequest<BusinessSettingsDto>;

internal sealed class GetBusinessSettingsHandler(IAppDbContext db) : IRequestHandler<GetBusinessSettingsQuery, BusinessSettingsDto>
{
    public Task<BusinessSettingsDto> Handle(GetBusinessSettingsQuery request, CancellationToken cancellationToken) => BuildAsync(db, cancellationToken);

    internal static async Task<BusinessSettingsDto> BuildAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var all = await db.AllSettingsAsync(null, cancellationToken);
        string Text(string key, string fallback) => all.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : fallback;
        var locales = all.TryGetValue(SettingKeys.LocalesEnabled, out var l) && l.ValueKind == JsonValueKind.Array
            ? l.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList()
            : ["fr"];
        var claimDays = all.TryGetValue(SettingKeys.ClaimResolutionDays, out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0;
        return new BusinessSettingsDto(
            Text(SettingKeys.ParcelCodePrefix, string.Empty),
            locales,
            Text(SettingKeys.LocalesDefault, "fr"),
            claimDays,
            Text(SettingKeys.LabelTemplate, "classic") == "branded" ? "branded" : "classic",
            Text(SettingKeys.LabelBrandColor, string.Empty),
            Text(SettingKeys.LabelBanner, string.Empty));
    }
}
