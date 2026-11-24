using System.Text.Json.Nodes;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Settings;

/// <summary>`rememberPrevious` : l'état courant devient « précédent » avant toute retouche du thème.</summary>
internal static class ThemeSnapshots
{
    public static JsonObject Of(string primary, string secondary, JsonObject theme) => new()
    {
        ["primaryColor"] = primary,
        ["secondaryColor"] = secondary,
        ["theme"] = theme,
    };

    public static async Task RememberPreviousAsync(IAppDbContext db, Company company, string? primary, string? secondary, JsonNode? theme, ILogger logger, CancellationToken cancellationToken)
    {
        var current = Of(company.PrimaryColor, company.SecondaryColor, ThemeOverrides.Prune(company.Theme));
        var target = Of(primary ?? company.PrimaryColor, secondary ?? company.SecondaryColor, theme is null ? ThemeOverrides.Prune(company.Theme) : ThemeOverrides.Prune(theme));
        if (current.ToJsonString() == target.ToJsonString()) return;
        try
        {
            await db.SetSettingAsync(company.Id, SettingKeys.ThemePrevious, current, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "thème précédent non mémorisé");
        }
    }
}
